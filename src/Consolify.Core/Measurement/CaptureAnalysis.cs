// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Measurement;

/// <summary>The analysed capture of one game.</summary>
/// <param name="Application">The game's executable name.</param>
/// <param name="Render">Frame times as the game produced them (between presents). Decisions use these.</param>
/// <param name="Display">Frame times as they reached the screen, when PresentMon tracked the display.</param>
/// <param name="DroppedFrames">Frames that never reached the screen.</param>
/// <param name="GeneratedFrames">Frames made by frame generation rather than rendered by the game.</param>
/// <param name="PresentModes">Present modes seen, most frequent first.</param>
public sealed record CaptureResult(
    string Application,
    FrameTimeStats Render,
    FrameTimeStats? Display,
    int DroppedFrames,
    int GeneratedFrames,
    IReadOnlyList<string> PresentModes)
{
    /// <summary>Generated frames make the capture useless for deciding a cap on real frames.</summary>
    public bool HasFrameGeneration => GeneratedFrames > 0;
}

/// <summary>Picks the game out of a capture and computes its statistics.</summary>
public static class CaptureAnalysis
{
    /// <summary>Processes that present frames but are never the game.</summary>
    private static readonly HashSet<string> SystemProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "dwm.exe", "explorer.exe", "ShellExperienceHost.exe", "StartMenuExperienceHost.exe",
        "SearchHost.exe", "TextInputHost.exe", "GameBar.exe", "GameBarFTServer.exe",
        "XboxPcApp.exe", "XboxPcAppFT.exe", "msedge.exe", "msedgewebview2.exe", "chrome.exe",
        "firefox.exe", "Discord.exe", "steamwebhelper.exe", "Claude.exe", "ArmouryCrateSE.exe",
        "PresentMon-2.6.0-x64.exe", "consolify-probe.exe",
    };

    /// <summary>Analyses the frames of one application.</summary>
    /// <param name="frames">All captured frames.</param>
    /// <param name="application">The game's executable name, or null to pick the busiest non-system process.</param>
    /// <exception cref="InvalidOperationException">No suitable application is in the capture.</exception>
    public static CaptureResult Analyze(IReadOnlyList<FrameRecord> frames, string? application = null)
    {
        string target = application ?? frames
            .Where(f => !SystemProcesses.Contains(f.Application))
            .GroupBy(f => f.Application, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No game was found in the capture.");

        // A game can recreate its swap chain or restart; use its busiest process only.
        var gameFrames = frames
            .Where(f => f.Application.Equals(target, StringComparison.OrdinalIgnoreCase))
            .GroupBy(f => f.ProcessId)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?
            .ToList();

        if (gameFrames is null || gameFrames.Count < 2)
        {
            throw new InvalidOperationException($"Not enough frames from {target} in the capture.");
        }

        var render = FrameTimeStats.From(gameFrames.Select(f => f.MsBetweenPresents));

        var displayTimes = gameFrames
            .Where(f => f.Displayed && f.MsBetweenDisplayChange is > 0)
            .Select(f => f.MsBetweenDisplayChange!.Value)
            .ToList();
        var display = displayTimes.Count >= 2 ? FrameTimeStats.From(displayTimes) : null;

        var modes = gameFrames
            .Where(f => !string.IsNullOrEmpty(f.PresentMode))
            .GroupBy(f => f.PresentMode!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();

        return new CaptureResult(
            Application: gameFrames[0].Application,
            Render: render,
            Display: display,
            DroppedFrames: gameFrames.Count(f => !f.Displayed),
            GeneratedFrames: gameFrames.Count(f => f.IsGenerated),
            PresentModes: modes);
    }
}
