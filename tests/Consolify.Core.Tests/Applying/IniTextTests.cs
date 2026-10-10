// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using Consolify.Core.Applying;

namespace Consolify.Core.Tests.Applying;

public class IniTextTests
{
    private const string OptiScaler =
        "[Upscalers]\r\nDx12Upscaler=fsr31\r\n\r\n[Framerate]\r\n\r\n; float - Default (auto) is 0.0 (disabled)\r\nFramerateLimit = auto\r\n\r\n[XeSS]\r\nBuildPipelines=auto\r\n";

    [Fact]
    public void Reads_a_value() =>
        Assert.Equal("auto", IniText.GetValue(OptiScaler, "Framerate", "FramerateLimit"));

    [Fact]
    public void Changes_only_the_value_and_keeps_everything_else()
    {
        string result = IniText.SetValue(OptiScaler, "Framerate", "FramerateLimit", "50");
        Assert.Equal(OptiScaler.Replace("FramerateLimit = auto", "FramerateLimit = 50"), result);
    }

    [Fact]
    public void Same_key_in_another_section_is_not_touched()
    {
        const string text = "[A]\nLimit=1\n[B]\nLimit=2\n";
        Assert.Equal("[A]\nLimit=1\n[B]\nLimit=9\n", IniText.SetValue(text, "B", "Limit", "9"));
    }

    [Fact]
    public void Missing_key_goes_under_its_section()
    {
        const string text = "[Framerate]\r\n; comment\r\n[XeSS]\r\n";
        Assert.Equal("[Framerate]\r\nFramerateLimit=50\r\n; comment\r\n[XeSS]\r\n",
            IniText.SetValue(text, "Framerate", "FramerateLimit", "50"));
    }

    [Fact]
    public void Missing_section_is_appended()
    {
        Assert.Equal("[A]\nx=1\n[Framerate]\nFramerateLimit=50\n",
            IniText.SetValue("[A]\nx=1\n", "Framerate", "FramerateLimit", "50"));
    }

    [Fact]
    public void Commented_out_key_is_not_a_value() =>
        Assert.Null(IniText.GetValue("[Framerate]\n;FramerateLimit=30\n", "Framerate", "FramerateLimit"));
}
