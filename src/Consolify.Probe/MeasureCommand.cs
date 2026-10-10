// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Globalization;
using System.Runtime.Versioning;
using Consolify.Core.Measurement;
using Consolify.Core.Pacing;
using Consolify.Windows.Display;
using Consolify.Windows.Measurement;

/// <summary>The <c>measure</c> and <c>analyze</c> commands of the probe.</summary>
[SupportedOSPlatform("windows")]
internal static class MeasureCommand
{
    /// <summary>
    /// Frame times this flat (99th percentile within this fraction of the median) usually mean a frame
    /// limiter or V-Sync is holding the game back, so the capture does not show what the game can do.
    /// </summary>
    private const double CappedFlatnessThreshold = 0.05;

    /// <summary>Spikes per minute above which the game itself is called stuttery. Starting value.</summary>
    private const double StutterSpikesPerMinute = 6;

    public static async Task<int> RunAsync(string[] args)
    {
        string? app = Option(args, "--app");
        string csvPath;

        if (args[0].Equals("analyze", StringComparison.OrdinalIgnoreCase))
        {
            if (args.Length < 2 || args[1].StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Usage: consolify-probe analyze capture.csv [--app Game.exe]");
                return 2;
            }

            csvPath = args[1];
        }
        else
        {
            int seconds = IntOption(args, "--seconds", 60);
            int delay = IntOption(args, "--delay", 10);

            Console.WriteLine("Consolify probe - frame time measurement");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Getting PresentMon {PresentMonRunner.Version} (official release, checksum verified)...");

            string presentMon;
            try
            {
                presentMon = await PresentMonRunner.EnsureAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidDataException or IOException)
            {
                Console.Error.WriteLine($"Could not get PresentMon: {ex.Message}");
                return 1;
            }

            string folder = PresentMonRunner.CapturesDirectory;
            Directory.CreateDirectory(folder);
            csvPath = Path.Combine(folder, $"capture-{DateTime.Now:yyyyMMdd-HHmmss}.csv");

            Console.WriteLine($"Recording starts in {delay} s and lasts {seconds} s.");
            Console.WriteLine("Run the game WITHOUT a frame limiter and play a demanding part.");
            if (!PresentMonRunner.IsElevated())
            {
                Console.WriteLine("Windows will ask for administrator rights: PresentMon needs them to trace frames.");
            }

            try
            {
                PresentMonRunner.Capture(presentMon, csvPath, seconds, delay, app);
            }
            catch (Exception ex) when (ex is OperationCanceledException or InvalidOperationException or TimeoutException)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        return Report(csvPath, app);
    }

    private static int Report(string csvPath, string? app)
    {
        CaptureResult result;
        try
        {
            using var reader = new StreamReader(csvPath);
            result = CaptureAnalysis.Analyze(PresentMonCsv.Parse(reader), app);
        }
        catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentException or IOException)
        {
            Console.Error.WriteLine($"Could not analyse {csvPath}: {ex.Message}");
            return 1;
        }

        var display = DisplayEnumerator.GetActiveDisplays()
            .OrderByDescending(d => d.IsPrimary)
            .FirstOrDefault();
        int refresh = display is null ? 0 : (int)Math.Round(display.RefreshHz);
        var vrr = display?.Edid?.GetEffectiveVrrRange(refresh);

        var r = result.Render;
        Console.WriteLine();
        Console.WriteLine($"{result.Application}");
        Console.WriteLine(new string('-', 60));
        Line("Frames / duration", $"{r.FrameCount} frames in {r.DurationSeconds:0.0} s");
        Line("Average", $"{r.AverageFps:0.0} FPS (informational only)");
        Line("1% low", $"{r.OnePercentLowFps:0.0} FPS  (99th percentile frame time {r.P99Ms:0.0} ms)");
        Line("0.1% low", $"{r.PointOnePercentLowFps:0.0} FPS  (worst frame {r.MaxMs:0.0} ms)");
        Line("Median frame time", $"{r.MedianMs:0.00} ms");
        Line("Spikes", $"{r.SpikeCount} (>= {FrameTimeStats.SpikeFactor:0}x median), {r.SpikesPerMinute:0.0} per minute");
        Line("Pacing (p95 change)", $"{r.PacingP95Ms:0.00} ms between consecutive frames");
        if (result.Display is { } shown)
        {
            Line("On screen 1% low", $"{shown.OnePercentLowFps:0.0} FPS, dropped frames: {result.DroppedFrames}");
        }

        if (result.PresentModes.Count > 0)
        {
            Line("Present mode", result.PresentModes[0]);
        }

        Console.WriteLine();
        Console.WriteLine("Display");
        Console.WriteLine(new string('-', 60));
        Line("Name", display?.FriendlyName ?? "unknown");
        Line("Refresh", $"{display?.RefreshHz.ToString("0.###", CultureInfo.InvariantCulture) ?? "?"} Hz");
        Line("VRR", vrr is null
            ? "none advertised"
            : $"{vrr.MinHz}-{vrr.MaxHz} Hz (capability from EDID; assumed active)");

        Console.WriteLine();
        Console.WriteLine("Decision");
        Console.WriteLine(new string('-', 60));

        bool looksCapped = r.MedianMs > 0 && (r.P99Ms - r.MedianMs) / r.MedianMs < CappedFlatnessThreshold;
        if (result.HasFrameGeneration)
        {
            Line("Not decided", $"{result.GeneratedFrames} generated frames found. Measure with frame generation OFF.");
            return 0;
        }

        if (looksCapped)
        {
            Line("Warning", $"Frame times are almost perfectly flat around {1000 / r.MedianMs:0.0} FPS.");
            Line(string.Empty, "A frame limiter or V-Sync is probably active, so this shows the cap,");
            Line(string.Empty, "not what the game can do. Remove the limiter and measure again.");
        }

        if (r.SpikesPerMinute > StutterSpikesPerMinute)
        {
            Line("Stutter", "The game itself hitches often (shader compilation, streaming).");
            Line(string.Empty, "Lowering image quality will not fix that.");
        }

        var target = FrameTargetPolicy.Decide(r.OnePercentLowFps, vrr, refresh);
        string verdict = target.Tier switch
        {
            ExperienceTier.Full => $"FULL EXPERIENCE - cap at {target.CapFps} FPS",
            ExperienceTier.Locked40Compromise => "LOCKED 40 - a compromise, cap at 40 FPS",
            _ => "NOT ENOUGH - not even a locked 40 holds at these settings",
        };
        Line("Result", verdict);
        Line("Rule", $"needs an uncapped 1% low of {target.RequiredOnePercentLow} FPS; measured {r.OnePercentLowFps:0.0}");
        Line("Capture file", csvPath);
        return 0;
    }

    private static void Line(string label, string value) => Console.WriteLine($"  {label,-20} {value}");

    private static string? Option(string[] args, string name)
    {
        int i = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static int IntOption(string[] args, string name, int fallback) =>
        int.TryParse(Option(args, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0 ? v : fallback;
}
