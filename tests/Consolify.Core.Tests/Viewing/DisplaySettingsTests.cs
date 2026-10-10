// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using Consolify.Core.Viewing;

namespace Consolify.Core.Tests.Viewing;

public class DisplaySettingsTests
{
    [Fact]
    public void Remembers_and_merges_values()
    {
        string path = Path.Combine(Path.GetTempPath(), $"consolify-test-{Guid.NewGuid():N}", "displays.json");
        var store = new DisplaySettingsStore(path);
        string key = DisplaySettingsStore.KeyFor("GSM", 0x82CD);
        Assert.Equal("GSM82CD", key);

        store.Set(key, new DisplaySettings(DiagonalInches: 83));
        store.Set(key, new DisplaySettings(DistanceCm: 280));

        var reloaded = new DisplaySettingsStore(path).Get(key);
        Assert.Equal(83, reloaded.DiagonalInches);
        Assert.Equal(280, reloaded.DistanceCm);
        Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
    }

    [Fact]
    public void Unknown_display_has_nothing_stored() =>
        Assert.Equal(new DisplaySettings(), new DisplaySettingsStore(Path.Combine(Path.GetTempPath(), $"none-{Guid.NewGuid():N}.json")).Get("X0000"));

    [Theory]
    [InlineData(DisplayKind.Television, false)]
    [InlineData(DisplayKind.Monitor, true)]
    [InlineData(DisplayKind.Internal, true)]
    public void Only_tv_sizes_need_asking(DisplayKind kind, bool trusted) =>
        Assert.Equal(trusted, DisplaySettingsStore.EdidSizeTrusted(kind));
}
