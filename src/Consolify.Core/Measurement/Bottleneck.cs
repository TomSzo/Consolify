// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Measurement;

/// <summary>Which part of the machine holds the frame rate back.</summary>
public enum BottleneckKind
{
    /// <summary>PresentMon did not report GPU busy time.</summary>
    Unknown,

    /// <summary>The GPU works nearly the whole frame: lighter graphics settings raise the frame rate.</summary>
    Gpu,

    /// <summary>The GPU waits for work: the CPU (or the game's own limits) sets the pace, not the graphics settings.</summary>
    Cpu,

    /// <summary>In between: both are close to their limit.</summary>
    Mixed,
}

/// <summary>Classifies a capture by how busy the GPU was relative to the frame time.</summary>
public static class Bottleneck
{
    /// <summary>GPU busy for at least this share of the frame means the GPU is the limit.</summary>
    public const double GpuBoundShare = 0.90;

    /// <summary>GPU busy for less than this share of the frame means something else is the limit.</summary>
    public const double CpuBoundShare = 0.75;

    /// <summary>Share of the median frame time the GPU was busy, or null when unknown.</summary>
    public static double? GpuShare(double medianFrameMs, double? medianGpuBusyMs) =>
        medianGpuBusyMs is > 0 && medianFrameMs > 0 ? medianGpuBusyMs.Value / medianFrameMs : null;

    public static BottleneckKind Classify(double medianFrameMs, double? medianGpuBusyMs) =>
        GpuShare(medianFrameMs, medianGpuBusyMs) switch
        {
            null => BottleneckKind.Unknown,
            >= GpuBoundShare => BottleneckKind.Gpu,
            < CpuBoundShare => BottleneckKind.Cpu,
            _ => BottleneckKind.Mixed,
        };
}
