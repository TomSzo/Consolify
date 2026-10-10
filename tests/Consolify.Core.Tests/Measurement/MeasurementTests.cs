// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Globalization;
using System.Text;
using Consolify.Core.Measurement;

namespace Consolify.Core.Tests.Measurement;

public class FrameTimeStatsTests
{
    [Fact]
    public void Flat_60_fps_has_no_spikes_and_no_pacing_error()
    {
        var stats = FrameTimeStats.From(Enumerable.Repeat(1000.0 / 60, 600));

        Assert.Equal(60, stats.AverageFps, precision: 3);
        Assert.Equal(60, stats.OnePercentLowFps, precision: 3);
        Assert.Equal(0, stats.SpikeCount);
        Assert.Equal(0, stats.PacingP95Ms, precision: 6);
        Assert.Equal(10, stats.DurationSeconds, precision: 3);
    }

    [Fact]
    public void One_percent_low_is_the_99th_percentile_frame_time()
    {
        // 990 frames at 16 ms, 10 frames at 25 ms: the 99th percentile is still 16 ms,
        // the 99.9th is 25 ms.
        var times = Enumerable.Repeat(16.0, 990).Concat(Enumerable.Repeat(25.0, 10));

        var stats = FrameTimeStats.From(times);

        Assert.Equal(16.0, stats.P99Ms);
        Assert.Equal(62.5, stats.OnePercentLowFps, precision: 3);
        Assert.Equal(25.0, stats.P999Ms);
        Assert.Equal(40.0, stats.PointOnePercentLowFps, precision: 3);
    }

    [Fact]
    public void An_average_of_60_can_hide_a_low_1_percent_low()
    {
        // Mostly fast frames with regular slow ones: the average looks fine, the 1% low does not.
        var times = Enumerable.Range(0, 1000).Select(i => i % 20 == 0 ? 45.0 : 15.0);

        var stats = FrameTimeStats.From(times);

        Assert.True(stats.AverageFps > 60);
        Assert.True(stats.OnePercentLowFps < 25);
        Assert.Equal(50, stats.SpikeCount);
    }

    [Fact]
    public void Alternating_frame_times_show_up_as_bad_pacing()
    {
        var stats = FrameTimeStats.From(Enumerable.Range(0, 200).Select(i => i % 2 == 0 ? 16.7 : 33.3));

        Assert.Equal(16.6, stats.PacingP95Ms, precision: 6);
    }

    [Fact]
    public void Ignores_non_positive_frame_times_and_needs_at_least_two()
    {
        Assert.Throws<ArgumentException>(() => FrameTimeStats.From([0, -1, 16.7]));
    }
}

public class PresentMonCsvTests
{
    private const string Header =
        "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
        "CPUStartTime,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI," +
        "MsRenderPresentLatency,MsUntilDisplayed,MsPCLatency,MsCPUBusy,MsCPUWait,MsGPULatency,MsGPUTime," +
        "MsGPUBusy,MsGPUWait,MsAnimationError,AnimationTime,MsAllInputToPhotonLatency,MsClickToPhotonLatency," +
        "DisplayLatency,DisplayedTime";

    private static string Row(string app, int pid, double present, string display, string displayed, string mode = "Hardware: Independent Flip") =>
        string.Join(',',
        [
            app, pid.ToString(CultureInfo.InvariantCulture), "0x0000ABCD", "DXGI", "0", "512", "1", $"\"{mode}\"",
            "1.0", "NA", present.ToString(CultureInfo.InvariantCulture), display, "0.1",
            "1.0", "2.0", "NA", "5.0", "1.0", "0.5", "10.0",
            "9.0", "1.0", "0.0", "NA", "NA", "NA",
            "20.0", displayed,
        ]);

    [Fact]
    public void Parses_columns_by_name_and_handles_na()
    {
        var csv = new StringBuilder()
            .AppendLine(Header)
            .AppendLine(Row("Game-Win64-Shipping.exe", 4242, 23.8, "23.8", "23.8"))
            .AppendLine(Row("Game-Win64-Shipping.exe", 4242, 23.9, "NA", "NA"))
            .ToString();

        var frames = PresentMonCsv.Parse(new StringReader(csv));

        Assert.Equal(2, frames.Count);
        Assert.Equal("Game-Win64-Shipping.exe", frames[0].Application);
        Assert.Equal(4242, frames[0].ProcessId);
        Assert.Equal(23.8, frames[0].MsBetweenPresents);
        Assert.Equal("Hardware: Independent Flip", frames[0].PresentMode);
        Assert.True(frames[0].Displayed);
        Assert.False(frames[1].Displayed);
        Assert.Null(frames[1].MsBetweenDisplayChange);
    }

    [Fact]
    public void Rejects_a_csv_without_the_required_columns()
    {
        Assert.Throws<FormatException>(() => PresentMonCsv.Parse(new StringReader("Application,ProcessID\nx,1\n")));
    }

    [Fact]
    public void Picks_the_busiest_non_system_process_as_the_game()
    {
        var csv = new StringBuilder().AppendLine(Header);
        for (int i = 0; i < 300; i++)
        {
            csv.AppendLine(Row("dwm.exe", 100, 8.3, "8.3", "8.3"));
        }

        for (int i = 0; i < 120; i++)
        {
            csv.AppendLine(Row("Game-Win64-Shipping.exe", 4242, 23.8, "23.8", "23.8"));
        }

        for (int i = 0; i < 20; i++)
        {
            csv.AppendLine(Row("Launcher.exe", 777, 33.3, "33.3", "33.3"));
        }

        var result = CaptureAnalysis.Analyze(PresentMonCsv.Parse(new StringReader(csv.ToString())));

        Assert.Equal("Game-Win64-Shipping.exe", result.Application);
        Assert.Equal(120, result.Render.FrameCount);
        Assert.Equal(1000.0 / 23.8, result.Render.OnePercentLowFps, precision: 3);
        Assert.NotNull(result.Display);
        Assert.Equal(0, result.DroppedFrames);
        Assert.False(result.HasFrameGeneration);
    }

    [Fact]
    public void Throws_when_no_game_is_in_the_capture()
    {
        var csv = new StringBuilder().AppendLine(Header).AppendLine(Row("dwm.exe", 100, 8.3, "8.3", "8.3")).ToString();

        Assert.Throws<InvalidOperationException>(() => CaptureAnalysis.Analyze(PresentMonCsv.Parse(new StringReader(csv))));
    }
}
