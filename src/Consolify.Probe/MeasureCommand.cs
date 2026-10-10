// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using Consolify.Core.Measurement;
using Consolify.Core.Pacing;
using Consolify.Windows.Applying;
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

    /// <summary>Hitch events per minute above which the game itself is called stuttery. Starting value.</summary>
    private const double StutterEventsPerMinute = 4;

    public static async Task<int> RunAsync(string[] args)
    {
        string? app = Option(args, "--app");
        var apply = new ApplyOptions(
            Enabled: args.Contains("--apply", StringComparer.OrdinalIgnoreCase),
            AssumeYes: args.Contains("--yes", StringComparer.OrdinalIgnoreCase),
            GameDir: Option(args, "--game-dir"));
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

        return Report(csvPath, app, apply);
    }

    /// <summary>The <c>restore</c> command: puts back the last file Consolify changed for a game.</summary>
    public static int Restore(string[] args)
    {
        string? app = Option(args, "--app");
        if (app is null)
        {
            Console.Error.WriteLine("Usage: consolify-probe restore --app Game.exe");
            return 2;
        }

        var restored = OptiScalerFrameCap.Restore(app);
        Console.WriteLine(restored is null
            ? $"Nothing to restore for {app}."
            : $"Restored {restored.OriginalPath} from {restored.BackupPath}. Restart the game for it to take effect.");
        return restored is null ? 1 : 0;
    }

    private sealed record ApplyOptions(bool Enabled, bool AssumeYes, string? GameDir);

    private static int Report(string csvPath, string? app, ApplyOptions apply)
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
        var hitch = result.Hitches;
        if (hitch is not null)
        {
            Line("Steady play", hitch.Steady is not { } st
                ? "none"
                : $"{hitch.SteadyShare:P0} of the capture, 1% low {st.OnePercentLowFps:0.0} FPS (median {1000 / st.MedianMs:0.0} FPS)");
            Line("Hitches", hitch.Hitches.Count == 0
                ? "none"
                : $"{hitch.Hitches.Count} ({hitch.Hitches.Sum(x => x.SlowFrames)} slow frames)");
            foreach (var x in hitch.Hitches.OrderByDescending(x => x.WorstMs).Take(5).OrderBy(x => x.StartSeconds))
            {
                Line(string.Empty, $"at {x.StartSeconds:0.0}-{x.EndSeconds:0.0} s: {x.SlowFrames} slow frame(s), worst {x.WorstMs:0} ms");
            }
        }
        Line("Pacing (p95 change)", $"{r.PacingP95Ms:0.00} ms between consecutive frames");
        if (result.Display is { } shown)
        {
            Line("On screen 1% low", $"{shown.OnePercentLowFps:0.0} FPS, dropped frames: {result.DroppedFrames}");
        }

        Line("Limited by", result.Bottleneck switch
        {
            BottleneckKind.Gpu => $"GPU (busy {result.MedianGpuBusyMs:0.0} of {r.MedianMs:0.0} ms per frame)",
            BottleneckKind.Cpu => $"CPU or the game itself (GPU busy only {result.MedianGpuBusyMs:0.0} of {r.MedianMs:0.0} ms)",
            BottleneckKind.Mixed => $"both CPU and GPU near their limit (GPU busy {result.MedianGpuBusyMs:0.0} of {r.MedianMs:0.0} ms)",
            _ => "unknown (PresentMon reported no GPU busy time)",
        });

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

        if (hitch?.Steady is not { } steady || !hitch.EnoughSteadyPlay)
        {
            Line("Not decided", "Too much of the capture is hitching (loading, streaming, menus).");
            Line(string.Empty, "Measure again during normal play.");
            Line("Capture file", csvPath);
            return 0;
        }

        double perMinute = r.DurationSeconds > 0 ? hitch.Hitches.Count / r.DurationSeconds * 60 : 0;
        if (perMinute > StutterEventsPerMinute)
        {
            Line("Stutter", "The game itself hitches often (shader compilation, streaming).");
            Line(string.Empty, "Lowering image quality will not fix that, so the cap below ignores it.");
        }
        else if (hitch.Hitches.Count > 0)
        {
            Line("Note", "Hitches are isolated (scene changes, loading): not a settings problem, left out of the decision.");
        }

        double steadyLow = steady.OnePercentLowFps;
        var target = FrameTargetPolicy.Decide(steadyLow, vrr, refresh);
        string verdict = target.Tier switch
        {
            ExperienceTier.Full => $"FULL EXPERIENCE - cap at {target.CapFps} FPS",
            ExperienceTier.Locked40Compromise => "LOCKED 40 - a compromise, cap at 40 FPS",
            _ => "NOT ENOUGH - not even a locked 40 holds at these settings",
        };
        Line("Result", verdict);
        Line("Rule", $"needs an uncapped steady 1% low of {target.RequiredOnePercentLow} FPS; measured {steadyLow:0.0}");
        if (target.Tier != ExperienceTier.Full)
        {
            Line("What now", result.Bottleneck switch
            {
                BottleneckKind.Gpu => "The GPU is the limit. Lighter graphics settings help - unless they are",
                BottleneckKind.Cpu => "The CPU is the limit. Lower resolution or upscaling will NOT help;",
                _ => "Lighter settings may help a little. If they are",
            });
            Line(string.Empty, result.Bottleneck switch
            {
                BottleneckKind.Gpu => "already at their lowest: then this hardware is the limit for this game.",
                BottleneckKind.Cpu => "only CPU-heavy settings (crowds, physics, view distance) or a faster CPU will.",
                _ => "already at their lowest, this hardware is the limit for this game.",
            });
        }

        Line("Capture file", csvPath);

        if (apply.Enabled && target.CapFps is int cap)
        {
            ApplyCap(result.Application, cap, apply);
        }

        return 0;
    }

    private static void ApplyCap(string gameExe, int cap, ApplyOptions apply)
    {
        Console.WriteLine();
        Console.WriteLine("Apply");
        Console.WriteLine(new string('-', 60));

        string? exePath = apply.GameDir is not null
            ? Path.Combine(apply.GameDir, gameExe)
            : FindRunningExe(gameExe);
        string? ini = exePath is null ? null : OptiScalerFrameCap.FindIni(exePath);
        if (ini is null)
        {
            Line("Skipped", exePath is null
                ? $"{gameExe} is not running, so its folder is unknown. Add --game-dir with the folder of the exe."
                : $"No OptiScaler next to {exePath}. Only OptiScaler's limiter is supported so far.");
            return;
        }

        double? now = OptiScalerFrameCap.ReadCap(ini);
        Line("Change", $"OptiScaler frame limit: {(now is null ? "none" : now.Value.ToString("0.#", CultureInfo.InvariantCulture))} -> {cap} FPS");
        Line("File", ini);
        if (now is double current && Math.Abs(current - cap) < 0.01)
        {
            Line("Result", "Already set. Nothing to do.");
            return;
        }

        if (!apply.AssumeYes)
        {
            if (Console.IsInputRedirected)
            {
                Line("Skipped", "No one to ask. Add --yes to apply without asking.");
                return;
            }

            Console.Write("  Apply it? A backup is made first. [y/N] ");
            if (!(Console.ReadLine()?.Trim().StartsWith('y') ?? false))
            {
                Line("Skipped", "Nothing changed.");
                return;
            }
        }

        try
        {
            var backup = OptiScalerFrameCap.Apply(ini, cap, gameExe);
            Line("Done", $"Frame limit set to {cap} FPS. It takes effect the next time the game starts.");
            Line("Backup", backup.BackupPath);
            Line("Undo", $"consolify-probe restore --app {gameExe}");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Line("Failed", $"Could not write the file: {ex.Message}");
        }
    }

    private static string? FindRunningExe(string gameExe)
    {
        foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(gameExe)))
        {
            using (process)
            {
                try
                {
                    if (process.MainModule?.FileName is { } path)
                    {
                        return path;
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Not allowed to read this process; try the next one.
                }
            }
        }

        return null;
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
