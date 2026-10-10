// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using Consolify.Core.Display;
using Consolify.Core.Viewing;

namespace Consolify.Windows.Display;

/// <summary>How the display is physically connected.</summary>
public enum DisplayConnection
{
    Unknown,
    Internal,
    Hdmi,
    DisplayPort,
    UsbC,
    Other,
}

/// <summary>An active display as Windows sees it, with its parsed EDID.</summary>
public sealed record ConnectedDisplay
{
    public required string FriendlyName { get; init; }
    public required string DevicePath { get; init; }
    public required DisplayConnection Connection { get; init; }
    public required uint OutputTechnologyRaw { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required double RefreshHz { get; init; }

    /// <summary>Whether this is the primary display (its desktop starts at 0,0).</summary>
    public required bool IsPrimary { get; init; }
    public EdidInfo? Edid { get; init; }
    public string? EdidError { get; init; }

    /// <summary>Best guess of how the display is used; internal panels are the device's own screen.</summary>
    public DisplayKind Kind => Connection == DisplayConnection.Internal
        ? DisplayKind.Internal
        : Edid?.DiagonalInches is >= 40 || (Edid is { HasHdmiBlock: true, WidthCm: 0 })
            ? DisplayKind.Television
            : DisplayKind.Monitor;
}
