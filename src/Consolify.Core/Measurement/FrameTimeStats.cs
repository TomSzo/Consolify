// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Measurement;

/// <summary>Frame time statistics for one capture.</summary>
/// <remarks>
/// "1% low" is defined as the frame rate of the 99th percentile frame time: 99% of frames finished at
/// least this fast. (Some tools instead average the slowest 1% of frames, which gives a lower number.)
/// Percentiles use the nearest-rank method.
/// </remarks>
public sealed record FrameTimeStats
{
    /// <summary>A frame at least this many times the median frame time counts as a spike.</summary>
    /// <remarks>Starting value, to be tuned with community measurements.</remarks>
    public const double SpikeFactor = 2.0;

    /// <summary>
    /// Spikes closer together than this belong to one hitch event (a scene change or a loading burst
    /// shows up as several slow frames in a row; it is still one hitch to the player).
    /// </summary>
    public const double SpikeEventGapMs = 1000.0;

    public required int FrameCount { get; init; }
    public required double DurationSeconds { get; init; }
    public required double AverageFps { get; init; }
    public required double MedianMs { get; init; }
    public required double P99Ms { get; init; }
    public required double P999Ms { get; init; }
    public required double MaxMs { get; init; }
    public required int SpikeCount { get; init; }

    /// <summary>Spikes grouped into hitches the player notices (see <see cref="SpikeEventGapMs"/>).</summary>
    public required int SpikeEventCount { get; init; }

    /// <summary>95th percentile of the change between consecutive frame times: frame pacing.</summary>
    public required double PacingP95Ms { get; init; }

    public double OnePercentLowFps => P99Ms > 0 ? 1000.0 / P99Ms : 0;
    public double PointOnePercentLowFps => P999Ms > 0 ? 1000.0 / P999Ms : 0;
    public double SpikesPerMinute => DurationSeconds > 0 ? SpikeCount / DurationSeconds * 60.0 : 0;
    public double SpikeEventsPerMinute => DurationSeconds > 0 ? SpikeEventCount / DurationSeconds * 60.0 : 0;

    /// <summary>Computes statistics from frame times in milliseconds, in capture order.</summary>
    /// <exception cref="ArgumentException">There are fewer than two usable frame times.</exception>
    public static FrameTimeStats From(IEnumerable<double> frameTimesMs)
    {
        var times = frameTimesMs.Where(t => t > 0 && double.IsFinite(t)).ToArray();
        if (times.Length < 2)
        {
            throw new ArgumentException("At least two frame times are needed.", nameof(frameTimesMs));
        }

        var sorted = times.Order().ToArray();
        double median = Percentile(sorted, 50);
        double total = times.Sum();

        var deltas = new double[times.Length - 1];
        for (int i = 1; i < times.Length; i++)
        {
            deltas[i - 1] = Math.Abs(times[i] - times[i - 1]);
        }

        Array.Sort(deltas);

        return new FrameTimeStats
        {
            FrameCount = times.Length,
            DurationSeconds = total / 1000.0,
            AverageFps = times.Length / (total / 1000.0),
            MedianMs = median,
            P99Ms = Percentile(sorted, 99),
            P999Ms = Percentile(sorted, 99.9),
            MaxMs = sorted[^1],
            SpikeCount = times.Count(t => t >= median * SpikeFactor),
            SpikeEventCount = CountSpikeEvents(times, median * SpikeFactor),
            PacingP95Ms = Percentile(deltas, 95),
        };
    }

    private static int CountSpikeEvents(double[] times, double threshold)
    {
        int events = 0;
        double clock = 0;
        double lastSpikeEnd = double.NegativeInfinity;
        foreach (double t in times)
        {
            clock += t;
            if (t < threshold)
            {
                continue;
            }

            // The spike started when the slow frame began, i.e. t before it ended.
            if (clock - t - lastSpikeEnd > SpikeEventGapMs)
            {
                events++;
            }

            lastSpikeEnd = clock;
        }

        return events;
    }

    /// <summary>Nearest-rank percentile of an ascending array.</summary>
    internal static double Percentile(double[] ascending, double percentile)
    {
        int rank = (int)Math.Ceiling(percentile / 100.0 * ascending.Length);
        return ascending[Math.Clamp(rank, 1, ascending.Length) - 1];
    }
}
