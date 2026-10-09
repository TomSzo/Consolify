// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Display;

/// <summary>The parts of a display's EDID that Consolify uses.</summary>
public sealed record EdidInfo
{
    /// <summary>Three-letter PNP manufacturer ID (e.g. "SAM", "LGD", "AUO").</summary>
    public required string ManufacturerId { get; init; }

    /// <summary>Manufacturer's product code.</summary>
    public required ushort ProductCode { get; init; }

    /// <summary>Monitor name from the EDID name descriptor, if present.</summary>
    public string? MonitorName { get; init; }

    /// <summary>EDID version, e.g. 1.4.</summary>
    public required Version EdidVersion { get; init; }

    /// <summary>Physical image width in centimetres (0 when unknown, e.g. projectors or some TVs).</summary>
    public required int WidthCm { get; init; }

    /// <summary>Physical image height in centimetres (0 when unknown).</summary>
    public required int HeightCm { get; init; }

    /// <summary>Whether the display declares continuous frequency support (EDID 1.4 feature bit).</summary>
    public required bool ContinuousFrequency { get; init; }

    /// <summary>Whether a CTA-861 HDMI vendor block is present (the display is connected as HDMI).</summary>
    public required bool HasHdmiBlock { get; init; }

    /// <summary>Whether the display advertises HDMI Auto Low-Latency Mode (game mode switching).</summary>
    public required bool SupportsAllm { get; init; }

    /// <summary>Every VRR range found, in the order it was found.</summary>
    public required IReadOnlyList<VrrRange> VrrRanges { get; init; }

    /// <summary>Number of 128-byte blocks that were parsed.</summary>
    public required int BlockCount { get; init; }

    /// <summary>Non-fatal problems found while parsing (bad checksums, truncated blocks, unparsed extensions).</summary>
    public required IReadOnlyList<string> Warnings { get; init; }

    /// <summary>Diagonal in inches computed from the physical size, or null when the size is unknown.</summary>
    public double? DiagonalInches =>
        WidthCm > 0 && HeightCm > 0
            ? Math.Sqrt(WidthCm * WidthCm + HeightCm * HeightCm) / 2.54
            : null;

    /// <summary>
    /// The VRR range Consolify should trust. Explicit vendor blocks are preferred over the generic
    /// range descriptor, and the highest minimum wins so that we never promise too much.
    /// </summary>
    /// <param name="currentRefreshHz">
    /// The active refresh rate. HDMI 2.1 displays may leave VRRmax empty, meaning "up to the refresh
    /// rate of the current video format"; this value fills that gap.
    /// </param>
    public VrrRange? GetEffectiveVrrRange(int? currentRefreshHz = null) =>
        VrrRanges
            .Select(r => r.MaxHz == 0 && currentRefreshHz is > 0 ? r with { MaxHz = currentRefreshHz.Value } : r)
            .Where(r => r.IsPlausible)
            .OrderBy(r => r.Source == VrrSource.MonitorRangeLimits ? 1 : 0)
            .ThenByDescending(r => r.MinHz)
            .FirstOrDefault();
}
