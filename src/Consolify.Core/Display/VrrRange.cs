// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Display;

/// <summary>Where in the display's EDID a variable refresh rate range was advertised.</summary>
public enum VrrSource
{
    /// <summary>EDID 1.4 Display Range Limits descriptor with continuous frequency ("range limits only").
    /// Used by VESA Adaptive-Sync / FreeSync over DisplayPort and most internal panels.</summary>
    MonitorRangeLimits,

    /// <summary>AMD vendor-specific data block in the CTA-861 extension (FreeSync, typically over HDMI).</summary>
    AmdFreeSync,

    /// <summary>HDMI Forum VSDB / SCDB VRRmin and VRRmax fields (HDMI 2.1 VRR, common on TVs).</summary>
    HdmiForumVrr,
}

/// <summary>A variable refresh rate window advertised by a display.</summary>
/// <param name="MinHz">Lowest refresh rate the display can follow.</param>
/// <param name="MaxHz">Highest refresh rate the display can follow.</param>
/// <param name="Source">Which EDID structure advertised this range.</param>
public sealed record VrrRange(int MinHz, int MaxHz, VrrSource Source)
{
    /// <summary>
    /// Max/min ratio at which drivers can usually apply low framerate compensation (frame doubling
    /// below the VRR window). Vendors use roughly 2.0 to 2.5; 2.0 is the optimistic bound.
    /// To be refined with community measurements.
    /// </summary>
    public const double LfcRatioThreshold = 2.0;

    /// <summary>Max/min ratio of the window.</summary>
    public double Ratio => MinHz > 0 ? (double)MaxHz / MinHz : 0;

    /// <summary>Whether the window is wide enough for low framerate compensation to be likely.</summary>
    public bool LikelySupportsLfc => Ratio >= LfcRatioThreshold;

    /// <summary>Whether this is a usable, plausible range.</summary>
    public bool IsPlausible => MinHz > 0 && MaxHz > MinHz && MaxHz <= 1000;
}
