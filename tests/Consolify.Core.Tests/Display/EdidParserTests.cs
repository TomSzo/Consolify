// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using Consolify.Core.Display;

namespace Consolify.Core.Tests.Display;

public class EdidParserTests
{
    [Fact]
    public void Reads_identity_and_physical_size()
    {
        var edid = new EdidBuilder("SAM", 0x7351).WithSize(60, 34).WithName("Odyssey G5").Build();

        var info = EdidParser.Parse(edid);

        Assert.Equal("SAM", info.ManufacturerId);
        Assert.Equal(0x7351, info.ProductCode);
        Assert.Equal("Odyssey G5", info.MonitorName);
        Assert.Equal(new Version(1, 4), info.EdidVersion);
        Assert.Equal(60, info.WidthCm);
        Assert.Equal(34, info.HeightCm);
        Assert.Equal(27.2, info.DiagonalInches!.Value, precision: 1);
        Assert.Empty(info.Warnings);
    }

    [Fact]
    public void Reads_range_limits_as_vrr_on_edid_1_4_with_continuous_frequency()
    {
        var edid = new EdidBuilder().WithContinuousFrequency().WithRangeLimits(48, 120).Build();

        var range = EdidParser.Parse(edid).GetEffectiveVrrRange();

        Assert.Equal(new VrrRange(48, 120, VrrSource.MonitorRangeLimits), range);
    }

    [Fact]
    public void Applies_range_limit_offsets_above_255_hz()
    {
        var edid = new EdidBuilder().WithContinuousFrequency().WithRangeLimits(48, 360).Build();

        Assert.Equal(360, EdidParser.Parse(edid).GetEffectiveVrrRange()!.MaxHz);
    }

    [Fact]
    public void Ignores_range_limits_without_continuous_frequency()
    {
        var edid = new EdidBuilder().WithRangeLimits(48, 120).Build();

        Assert.Null(EdidParser.Parse(edid).GetEffectiveVrrRange());
    }

    [Fact]
    public void Ignores_range_limits_that_are_a_timing_formula()
    {
        var edid = new EdidBuilder().WithContinuousFrequency().WithRangeLimits(48, 120, flags: 0x00).Build();

        Assert.Null(EdidParser.Parse(edid).GetEffectiveVrrRange());
    }

    [Fact]
    public void Reads_hdmi_forum_vrr_and_allm_from_a_tv()
    {
        var edid = new EdidBuilder("GSM", minorVersion: 3)
            .WithCtaExtension(EdidBuilder.HdmiLlcBlock(), EdidBuilder.HdmiForumBlock(40, 120))
            .Build();

        var info = EdidParser.Parse(edid);

        Assert.True(info.HasHdmiBlock);
        Assert.True(info.SupportsAllm);
        Assert.Equal(new VrrRange(40, 120, VrrSource.HdmiForumVrr), info.GetEffectiveVrrRange());
    }

    [Fact]
    public void Fills_an_empty_hdmi_vrr_max_from_the_current_refresh_rate()
    {
        var edid = new EdidBuilder(minorVersion: 3)
            .WithCtaExtension(EdidBuilder.HdmiLlcBlock(), EdidBuilder.HdmiForumBlock(48, 0))
            .Build();

        var info = EdidParser.Parse(edid);

        Assert.Null(info.GetEffectiveVrrRange());
        Assert.Equal(new VrrRange(48, 60, VrrSource.HdmiForumVrr), info.GetEffectiveVrrRange(currentRefreshHz: 60));
    }

    [Theory]
    [InlineData(2, 48, 144)]
    [InlineData(3, 48, 500)]
    public void Reads_amd_freesync_block(int version, int min, int max)
    {
        var edid = new EdidBuilder(minorVersion: 3)
            .WithCtaExtension(EdidBuilder.AmdBlock(version, min, max))
            .Build();

        Assert.Equal(new VrrRange(min, max, VrrSource.AmdFreeSync), EdidParser.Parse(edid).GetEffectiveVrrRange());
    }

    [Fact]
    public void Prefers_vendor_blocks_over_the_generic_range_descriptor()
    {
        var edid = new EdidBuilder()
            .WithContinuousFrequency()
            .WithRangeLimits(40, 144)
            .WithCtaExtension(EdidBuilder.AmdBlock(2, 48, 144))
            .Build();

        var info = EdidParser.Parse(edid);

        Assert.Equal(2, info.VrrRanges.Count);
        Assert.Equal(VrrSource.AmdFreeSync, info.GetEffectiveVrrRange()!.Source);
    }

    [Fact]
    public void Rejects_data_without_an_edid_header()
    {
        Assert.Throws<EdidFormatException>(() => EdidParser.Parse(new byte[128]));
    }

    [Fact]
    public void Warns_about_a_bad_checksum_but_still_parses()
    {
        var edid = new EdidBuilder().WithSize(60, 34).Build();
        edid[127] ^= 0xFF;

        var info = EdidParser.Parse(edid);

        Assert.Equal(60, info.WidthCm);
        Assert.Contains(info.Warnings, w => w.Contains("checksum"));
    }

    [Fact]
    public void Warns_when_extensions_are_missing()
    {
        var edid = new EdidBuilder().WithCtaExtension(EdidBuilder.HdmiLlcBlock()).Build()[..128];

        var info = EdidParser.Parse(edid);

        Assert.Equal(1, info.BlockCount);
        Assert.Contains(info.Warnings, w => w.Contains("extension"));
    }
}
