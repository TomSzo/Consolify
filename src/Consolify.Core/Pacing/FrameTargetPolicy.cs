// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using Consolify.Core.Display;

namespace Consolify.Core.Pacing;

/// <summary>How good an experience a game can deliver on this PC and display.</summary>
public enum ExperienceTier
{
    /// <summary>The full experience: a frame cap the game holds with flat frame times.</summary>
    Full,

    /// <summary>A locked 40 FPS. Smooth, but a compromise; the player is told so.</summary>
    Locked40Compromise,

    /// <summary>Not even a locked 40 holds at console quality: the honest hardware verdict.</summary>
    InsufficientHardware,
}

/// <summary>The decision for one game on one display.</summary>
/// <param name="Tier">Which experience the player gets.</param>
/// <param name="CapFps">The frame cap to apply, or null when the hardware is insufficient.</param>
/// <param name="RequiredOnePercentLow">The uncapped 1% low the tier requires.</param>
public sealed record FrameTarget(ExperienceTier Tier, int? CapFps, int RequiredOnePercentLow);

/// <summary>
/// Turns a measured, uncapped 1% low into a frame cap.
/// </summary>
/// <remarks>
/// <para>
/// The game is measured without a cap. The cap goes one frame below the measured 1% low, so the game
/// almost always finishes a frame before the cap's deadline and frame times stay flat.
/// </para>
/// <para>
/// Full experience, with VRR: the 1% low must reach the display's VRR minimum + 1, and at least 41,
/// so the picture always stays inside the VRR window. Without VRR: at least 61, for a locked 60.
/// </para>
/// <para>
/// The only fallback is a locked 40 (1% low at least 41), and only where it can be shown evenly:
/// a fixed 120 Hz cadence, or VRR whose window reaches 40 or that can double frames below it (LFC).
/// There is no 30 FPS mode. Below the fallback comes the honest hardware verdict.
/// </para>
/// </remarks>
public static class FrameTargetPolicy
{
    /// <summary>Frames per second kept between the measured 1% low and the cap.</summary>
    public const int MarginFps = 1;

    /// <summary>Highest cap without frame generation. Headroom above it goes into image quality.</summary>
    public const int MaxCapFps = 60;

    /// <summary>Lowest acceptable uncapped 1% low with VRR, whatever the display's own minimum.</summary>
    public const int VrrAbsoluteFloor = 41;

    /// <summary>Uncapped 1% low required for a locked 60 without VRR.</summary>
    public const int FixedRefreshFullFloor = MaxCapFps + MarginFps;

    /// <summary>The fallback cap.</summary>
    public const int FallbackCapFps = 40;

    /// <summary>Uncapped 1% low required for the locked 40 fallback.</summary>
    public const int FallbackFloor = FallbackCapFps + MarginFps;

    /// <summary>The uncapped 1% low a game needs for the full experience on this display.</summary>
    public static int FullExperienceFloor(VrrRange? vrr) =>
        vrr is null ? FixedRefreshFullFloor : Math.Max(vrr.MinHz + MarginFps, VrrAbsoluteFloor);

    /// <summary>Whether this display can show a locked 40 evenly.</summary>
    public static bool CanShowLocked40(VrrRange? vrr, int refreshHz) =>
        vrr is not null
            ? vrr.MinHz <= FallbackCapFps || vrr.LikelySupportsLfc
            : refreshHz > 0 && refreshHz % 120 == 0;

    /// <summary>Picks the tier and cap for a game from its uncapped 1% low.</summary>
    /// <param name="uncappedOnePercentLow">Measured 1% low in FPS, without any frame cap.</param>
    /// <param name="vrr">The display's active VRR range, or null when VRR is off or unsupported.</param>
    /// <param name="refreshHz">The display's current refresh rate.</param>
    public static FrameTarget Decide(double uncappedOnePercentLow, VrrRange? vrr, int refreshHz)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(uncappedOnePercentLow);
        int low = (int)Math.Floor(uncappedOnePercentLow);
        int fullFloor = FullExperienceFloor(vrr);

        if (low >= fullFloor)
        {
            if (vrr is not null)
            {
                int cap = Math.Min(MaxCapFps, Math.Min(low - MarginFps, vrr.MaxHz));
                return new FrameTarget(ExperienceTier.Full, cap, fullFloor);
            }

            // A locked 60 needs a refresh rate that 60 divides evenly (60, 120, 240 Hz).
            if (refreshHz > 0 && refreshHz % MaxCapFps == 0)
            {
                return new FrameTarget(ExperienceTier.Full, MaxCapFps, fullFloor);
            }
        }

        if (low >= FallbackFloor && CanShowLocked40(vrr, refreshHz))
        {
            return new FrameTarget(ExperienceTier.Locked40Compromise, FallbackCapFps, FallbackFloor);
        }

        return new FrameTarget(ExperienceTier.InsufficientHardware, null, fullFloor);
    }
}
