// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using Consolify.Core.Display;
using Consolify.Core.Pacing;

namespace Consolify.Core.Tests.Pacing;

public class FrameTargetPolicyTests
{
    private static readonly VrrRange HandheldPanel = new(48, 120, VrrSource.MonitorRangeLimits);
    private static readonly VrrRange Tv = new(40, 120, VrrSource.HdmiForumVrr);
    private static readonly VrrRange NarrowMonitor = new(48, 75, VrrSource.AmdFreeSync);
    private static readonly VrrRange GSyncModule = new(1, 144, VrrSource.MonitorRangeLimits);

    [Theory]
    [InlineData(48, 120, 49)]
    [InlineData(40, 120, 41)]
    [InlineData(1, 144, 41)]
    public void Full_experience_floor_with_vrr_is_vrr_minimum_plus_one_but_at_least_41(int min, int max, int expected)
    {
        Assert.Equal(expected, FrameTargetPolicy.FullExperienceFloor(new VrrRange(min, max, VrrSource.MonitorRangeLimits)));
    }

    [Fact]
    public void Full_experience_floor_without_vrr_is_61()
    {
        Assert.Equal(61, FrameTargetPolicy.FullExperienceFloor(null));
    }

    [Theory]
    [InlineData(52.4, 51)]
    [InlineData(49.0, 48)]
    [InlineData(75.0, 60)]
    public void With_vrr_the_cap_sits_one_frame_below_the_uncapped_1_percent_low(double low, int expectedCap)
    {
        var target = FrameTargetPolicy.Decide(low, HandheldPanel, refreshHz: 120);

        Assert.Equal(ExperienceTier.Full, target.Tier);
        Assert.Equal(expectedCap, target.CapFps);
    }

    [Fact]
    public void A_tv_with_a_40_hz_vrr_minimum_gives_the_full_experience_from_41()
    {
        var target = FrameTargetPolicy.Decide(41.0, Tv, refreshHz: 120);

        Assert.Equal(new FrameTarget(ExperienceTier.Full, 40, 41), target);
    }

    [Fact]
    public void Handheld_below_its_vrr_floor_falls_back_to_a_locked_40_through_lfc()
    {
        var target = FrameTargetPolicy.Decide(45.0, HandheldPanel, refreshHz: 120);

        Assert.Equal(new FrameTarget(ExperienceTier.Locked40Compromise, 40, 41), target);
    }

    [Fact]
    public void Narrow_vrr_window_without_lfc_cannot_show_a_locked_40()
    {
        var target = FrameTargetPolicy.Decide(45.0, NarrowMonitor, refreshHz: 75);

        Assert.Equal(ExperienceTier.InsufficientHardware, target.Tier);
        Assert.Null(target.CapFps);
    }

    [Fact]
    public void G_sync_module_never_accepts_less_than_41()
    {
        Assert.Equal(ExperienceTier.InsufficientHardware, FrameTargetPolicy.Decide(35.0, GSyncModule, 144).Tier);
        Assert.Equal(ExperienceTier.Full, FrameTargetPolicy.Decide(41.0, GSyncModule, 144).Tier);
    }

    [Theory]
    [InlineData(61.0, 60, ExperienceTier.Full, 60)]
    [InlineData(60.9, 120, ExperienceTier.Locked40Compromise, 40)]
    [InlineData(60.9, 60, ExperienceTier.InsufficientHardware, null)]
    [InlineData(45.0, 120, ExperienceTier.Locked40Compromise, 40)]
    [InlineData(40.5, 120, ExperienceTier.InsufficientHardware, null)]
    public void Without_vrr_a_locked_60_needs_61_and_a_locked_40_needs_a_120_hz_display(
        double low, int refresh, ExperienceTier expectedTier, int? expectedCap)
    {
        var target = FrameTargetPolicy.Decide(low, vrr: null, refreshHz: refresh);

        Assert.Equal(expectedTier, target.Tier);
        Assert.Equal(expectedCap, target.CapFps);
    }

    [Fact]
    public void Without_vrr_a_locked_60_is_not_offered_on_a_refresh_rate_60_does_not_divide()
    {
        Assert.Equal(ExperienceTier.InsufficientHardware, FrameTargetPolicy.Decide(90.0, vrr: null, refreshHz: 75).Tier);
    }

    [Fact]
    public void There_is_no_30_fps_mode()
    {
        Assert.Equal(ExperienceTier.InsufficientHardware, FrameTargetPolicy.Decide(35.0, HandheldPanel, 120).Tier);
    }
}
