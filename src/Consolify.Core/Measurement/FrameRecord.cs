// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Measurement;

/// <summary>One presented frame, as captured by PresentMon.</summary>
/// <param name="Application">Executable name of the process that presented the frame.</param>
/// <param name="ProcessId">Process ID.</param>
/// <param name="MsBetweenPresents">Time since the previous present: how fast the game produces frames.</param>
/// <param name="MsBetweenDisplayChange">How long the previous frame stayed on screen, or null if unknown.</param>
/// <param name="Displayed">Whether the frame reached the screen at all.</param>
/// <param name="FrameType">Application-rendered or generated (frame generation), when PresentMon knows it.</param>
/// <param name="PresentMode">How the frame was presented (e.g. "Hardware: Independent Flip").</param>
/// <param name="MsGpuBusy">Time the GPU spent working on this frame, or null if PresentMon did not report it.</param>
/// <param name="MsCpuBusy">Time the CPU spent on this frame before presenting it, or null if unknown.</param>
public sealed record FrameRecord(
    string Application,
    int ProcessId,
    double MsBetweenPresents,
    double? MsBetweenDisplayChange,
    bool Displayed,
    string? FrameType,
    string? PresentMode,
    double? MsGpuBusy = null,
    double? MsCpuBusy = null)
{
    /// <summary>Whether a driver or SDK generated this frame instead of the game rendering it.</summary>
    public bool IsGenerated =>
        FrameType is not null
        && !FrameType.Equals("Application", StringComparison.OrdinalIgnoreCase)
        && !FrameType.Equals("NA", StringComparison.OrdinalIgnoreCase);
}
