// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Consolify.Core.Display;
using Consolify.Core.Pacing;
using Consolify.Core.Viewing;
using Consolify.Windows.Display;

// consolify-probe: prints what Consolify detects and measures. It changes nothing unless --apply is given.
//
// Usage:
//   consolify-probe                  display report (human-readable)
//   consolify-probe --json           display report (machine-readable)
//   consolify-probe --distance 250   assume this viewing distance (cm) for every display
//   consolify-probe measure [--seconds 60] [--delay 10] [--app Game.exe]
//                                    record a game with PresentMon and print the decision
//   consolify-probe analyze capture.csv [--app Game.exe]
//                                    analyse an existing PresentMon capture
//   ... measure|analyze ... --apply [--yes] [--game-dir DIR]
//                                    also write the frame cap to the game's OptiScaler.ini (after a backup)
//   consolify-probe restore --app Game.exe
//                                    put back the last file Consolify changed for that game

if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
{
    Console.Error.WriteLine("consolify-probe needs Windows 10 2004 or newer.");
    return 1;
}

if (args.Length > 0 && (args[0].Equals("measure", StringComparison.OrdinalIgnoreCase)
    || args[0].Equals("analyze", StringComparison.OrdinalIgnoreCase)))
{
    return await MeasureCommand.RunAsync(args);
}

if (args.Length > 0 && args[0].Equals("restore", StringComparison.OrdinalIgnoreCase))
{
    return MeasureCommand.Restore(args);
}

bool json = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
double? distanceOverride = ReadDistance(args);

var displays = DisplayEnumerator.GetActiveDisplays();
var settings = WindowsGraphicsSettings.ReadGlobalSettings();

var reports = displays.Select(d => BuildReport(d, distanceOverride)).ToList();

if (json)
{
    var options = new JsonSerializerOptions
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
    Console.WriteLine(JsonSerializer.Serialize(new { Displays = reports, WindowsDirectXSettings = settings }, options));
    return 0;
}

Console.WriteLine("Consolify probe - display detection (read-only)");
Console.WriteLine(new string('=', 60));

foreach (var report in reports)
{
    PrintReport(report);
}

Console.WriteLine("Windows DirectX user settings");
Console.WriteLine(new string('-', 60));
if (settings.Count == 0)
{
    Console.WriteLine("  (none set - Windows defaults apply)");
}
else
{
    foreach (var (key, value) in settings)
    {
        Console.WriteLine($"  {key} = {value}");
    }
}

return 0;

static double? ReadDistance(string[] args)
{
    int index = Array.FindIndex(args, a => a.Equals("--distance", StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < args.Length
        && double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double cm) && cm > 0
        ? cm
        : null;
}

static DisplayReport BuildReport(ConnectedDisplay display, double? distanceOverride)
{
    var edid = display.Edid;
    int refresh = (int)Math.Round(display.RefreshHz);
    var vrr = edid?.GetEffectiveVrrRange(refresh);

    double? widthCm = edid is { WidthCm: > 0 } ? edid.WidthCm : null;
    double distance = distanceOverride ?? ViewingGeometry.DefaultDistanceCm(display.Kind, edid?.DiagonalInches);
    double? ppd = widthCm is not null && display.Width > 0
        ? ViewingGeometry.PixelsPerDegree(display.Width, widthCm.Value, distance)
        : null;

    return new DisplayReport(
        Name: display.FriendlyName,
        Connection: display.Connection,
        OutputTechnologyRaw: $"0x{display.OutputTechnologyRaw:X8}",
        Kind: display.Kind,
        Resolution: $"{display.Width}x{display.Height}",
        RefreshHz: display.RefreshHz,
        Manufacturer: edid?.ManufacturerId,
        ProductCode: edid is null ? null : $"0x{edid.ProductCode:X4}",
        EdidVersion: edid?.EdidVersion.ToString(),
        SizeCm: edid is { WidthCm: > 0 } ? $"{edid.WidthCm}x{edid.HeightCm}" : null,
        DiagonalInches: edid?.DiagonalInches is double d ? Math.Round(d, 1) : null,
        HdmiSink: edid?.HasHdmiBlock,
        Allm: edid?.SupportsAllm,
        VrrRangesFound: edid?.VrrRanges.Select(r => $"{r.MinHz}-{(r.MaxHz == 0 ? "?" : r.MaxHz)} Hz ({r.Source})").ToList(),
        EffectiveVrr: vrr is null ? null : $"{vrr.MinHz}-{vrr.MaxHz} Hz ({vrr.Source})",
        LikelyLfc: vrr?.LikelySupportsLfc,
        FullExperienceFloor: FrameTargetPolicy.FullExperienceFloor(vrr),
        Locked40Fallback: FrameTargetPolicy.CanShowLocked40(vrr, refresh),
        AssumedDistanceCm: distance,
        PixelsPerDegree: ppd is double p ? Math.Round(p, 1) : null,
        UpscaleCeiling: ppd is double q ? ViewingGeometry.CeilingFor(q) : null,
        EdidWarnings: edid?.Warnings.Count > 0 ? edid.Warnings : null,
        EdidError: display.EdidError);
}

static void PrintReport(DisplayReport r)
{
    Console.WriteLine($"{r.Name}");
    Console.WriteLine(new string('-', 60));
    Line("Connection", $"{r.Connection} ({r.OutputTechnologyRaw}), treated as: {r.Kind}");
    Line("Mode", $"{r.Resolution} @ {r.RefreshHz.ToString("0.###", CultureInfo.InvariantCulture)} Hz");
    Line("EDID", r.EdidError is not null
        ? $"unavailable - {r.EdidError}"
        : $"{r.Manufacturer} {r.ProductCode}, version {r.EdidVersion}");
    Line("Physical size", r.SizeCm is null ? "unknown" : $"{r.SizeCm} cm ({r.DiagonalInches}\")");
    Line("HDMI sink / ALLM", $"{Yes(r.HdmiSink)} / {Yes(r.Allm)}");

    if (r.VrrRangesFound is { Count: > 0 })
    {
        Line("VRR ranges found", string.Join(", ", r.VrrRangesFound));
    }

    if (r.EffectiveVrr is null)
    {
        Line("VRR", "none advertised - fixed refresh: a locked 60 needs an uncapped 1% low of 61");
    }
    else
    {
        Line("VRR (trusted)", r.EffectiveVrr);
        Line("LFC likely", Yes(r.LikelyLfc));
    }

    Line("Full experience", $"uncapped 1% low >= {r.FullExperienceFloor} FPS, cap 1 FPS below it (max 60)");
    Line("Locked 40 fallback", r.Locked40Fallback ? "possible (shown as a compromise)" : "not possible on this display");

    Line("Viewing distance", $"{r.AssumedDistanceCm} cm (assumed; override with --distance)");
    Line("Pixels per degree", r.PixelsPerDegree is null ? "unknown (no physical size)" : $"{r.PixelsPerDegree} -> upscale ceiling: {r.UpscaleCeiling}");

    foreach (var warning in r.EdidWarnings ?? [])
    {
        Line("EDID note", warning);
    }

    Console.WriteLine();

    static void Line(string label, string value) => Console.WriteLine($"  {label,-18} {value}");
    static string Yes(bool? value) => value switch { true => "yes", false => "no", null => "unknown" };
}

internal sealed record DisplayReport(
    string Name,
    DisplayConnection Connection,
    string OutputTechnologyRaw,
    DisplayKind Kind,
    string Resolution,
    double RefreshHz,
    string? Manufacturer,
    string? ProductCode,
    string? EdidVersion,
    string? SizeCm,
    double? DiagonalInches,
    bool? HdmiSink,
    bool? Allm,
    IReadOnlyList<string>? VrrRangesFound,
    string? EffectiveVrr,
    bool? LikelyLfc,
    int FullExperienceFloor,
    bool Locked40Fallback,
    double AssumedDistanceCm,
    double? PixelsPerDegree,
    UpscaleCeiling? UpscaleCeiling,
    IReadOnlyList<string>? EdidWarnings,
    string? EdidError);
