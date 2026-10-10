// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Measurement;

/// <summary>One hitch the player notices: a burst of frames far slower than the frames just before.</summary>
/// <param name="StartSeconds">When the first slow frame began, from the start of the capture.</param>
/// <param name="EndSeconds">When the last slow frame ended.</param>
/// <param name="SlowFrames">How many slow frames the burst had.</param>
/// <param name="WorstMs">The slowest frame in the burst.</param>
public sealed record Hitch(double StartSeconds, double EndSeconds, int SlowFrames, double WorstMs);

/// <summary>
/// Separates hitches (shader compilation, streaming, loading) from steady play.
/// </summary>
/// <remarks>
/// Consolify's rule is that stutter is the game's fault and never a reason to lower image quality.
/// So the frame cap is decided from the steady part of the capture, and hitches are reported on
/// their own. A frame is slow when it takes at least <see cref="FrameTimeStats.SpikeFactor"/> times
/// the median of the second before it, so a gradually heavier scene is still steady play: only
/// sudden jumps count. Slow frames less than <see cref="FrameTimeStats.SpikeEventGapMs"/> apart are
/// one hitch, and <see cref="PaddingMs"/> around each hitch is left out of the steady part too.
/// </remarks>
public sealed record HitchAnalysis(IReadOnlyList<Hitch> Hitches, FrameTimeStats? Steady, double SteadyShare)
{
    /// <summary>Time left out before and after each hitch (recovery frames are not steady play).</summary>
    public const double PaddingMs = 500;

    /// <summary>How far back the local baseline looks.</summary>
    public const double BaselineWindowMs = 1000;

    /// <summary>Below this share of steady play the capture is not good enough to decide from.</summary>
    public const double MinSteadyShare = 0.6;

    public bool EnoughSteadyPlay => Steady is not null && SteadyShare >= MinSteadyShare;

    /// <summary>Analyses frame times in milliseconds, in capture order.</summary>
    public static HitchAnalysis Analyze(IReadOnlyList<double> frameTimesMs)
    {
        var times = frameTimesMs.Where(t => t > 0 && double.IsFinite(t)).ToArray();
        var ends = new double[times.Length];
        double clock = 0;
        for (int i = 0; i < times.Length; i++)
        {
            clock += times[i];
            ends[i] = clock;
        }

        // Mark frames that are much slower than the second before them.
        var slow = new bool[times.Length];
        int windowStart = 0;
        for (int i = 1; i < times.Length; i++)
        {
            double start = ends[i] - times[i];
            while (windowStart < i - 1 && ends[windowStart] < start - BaselineWindowMs)
            {
                windowStart++;
            }

            var window = times[windowStart..i];
            Array.Sort(window);
            double baseline = window[window.Length / 2];
            slow[i] = times[i] >= baseline * FrameTimeStats.SpikeFactor;
        }

        // Group slow frames into hitches.
        var hitches = new List<Hitch>();
        double hStart = 0, hEnd = 0, worst = 0;
        int count = 0;
        for (int i = 0; i < times.Length; i++)
        {
            if (!slow[i])
            {
                continue;
            }

            double start = ends[i] - times[i];
            if (count > 0 && start - hEnd > FrameTimeStats.SpikeEventGapMs)
            {
                hitches.Add(new Hitch(hStart / 1000, hEnd / 1000, count, worst));
                count = 0;
            }

            if (count == 0)
            {
                hStart = start;
                worst = 0;
            }

            hEnd = ends[i];
            worst = Math.Max(worst, times[i]);
            count++;
        }

        if (count > 0)
        {
            hitches.Add(new Hitch(hStart / 1000, hEnd / 1000, count, worst));
        }

        // Steady play: everything outside the padded hitches.
        var steady = new List<double>(times.Length);
        int h = 0;
        for (int i = 0; i < times.Length; i++)
        {
            double start = ends[i] - times[i];
            while (h < hitches.Count && hitches[h].EndSeconds * 1000 + PaddingMs < start)
            {
                h++;
            }

            bool inHitch = h < hitches.Count
                && ends[i] > hitches[h].StartSeconds * 1000 - PaddingMs
                && start < hitches[h].EndSeconds * 1000 + PaddingMs;
            if (!inHitch)
            {
                steady.Add(times[i]);
            }
        }

        double total = clock;
        return steady.Count < 2
            ? new HitchAnalysis(hitches, null, 0)
            : new HitchAnalysis(hitches, FrameTimeStats.From(steady), total > 0 ? steady.Sum() / total : 0);
    }
}
