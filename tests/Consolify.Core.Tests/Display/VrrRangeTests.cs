// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using Consolify.Core.Display;

namespace Consolify.Core.Tests.Display;

public class VrrRangeTests
{
    [Fact]
    public void Handheld_panel_48_to_120_keeps_a_floor_and_cap_inside_the_window()
    {
        var range = new VrrRange(48, 120, VrrSource.MonitorRangeLimits);

        Assert.Equal(51, range.OnePercentLowFloor);
        Assert.Equal(117, range.RecommendedFrameCap);
        Assert.True(range.LikelySupportsLfc);
    }

    [Fact]
    public void Narrow_window_does_not_promise_lfc()
    {
        Assert.False(new VrrRange(48, 75, VrrSource.AmdFreeSync).LikelySupportsLfc);
    }

    [Theory]
    [InlineData(0, 120)]
    [InlineData(120, 48)]
    [InlineData(48, 0)]
    public void Rejects_implausible_ranges(int min, int max)
    {
        Assert.False(new VrrRange(min, max, VrrSource.HdmiForumVrr).IsPlausible);
    }
}
