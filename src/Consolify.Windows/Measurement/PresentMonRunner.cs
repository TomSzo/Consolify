// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Principal;

namespace Consolify.Windows.Measurement;

/// <summary>
/// Downloads the official PresentMon console build, verifies it, and runs captures with it.
/// PresentMon is never bundled: it comes from its official GitHub release with a pinned checksum.
/// </summary>
[SupportedOSPlatform("windows")]
public static class PresentMonRunner
{
    public const string Version = "2.6.0";
    public const string FileName = "PresentMon-2.6.0-x64.exe";
    public const string DownloadUrl = "https://github.com/GameTechDev/PresentMon/releases/download/v2.6.0/PresentMon-2.6.0-x64.exe";

    /// <summary>SHA-256 published on the v2.6.0 release page; checked again on every run.</summary>
    public const string Sha256 = "b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af";

    private const int ErrorCancelled = 1223;

    /// <summary>Where Consolify keeps downloaded tools.</summary>
    public static string ToolsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Consolify", "tools");

    /// <summary>Returns the path to a verified PresentMon, downloading it first if needed.</summary>
    /// <exception cref="InvalidDataException">The file does not match the pinned checksum.</exception>
    public static async Task<string> EnsureAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(ToolsDirectory);
        string path = Path.Combine(ToolsDirectory, FileName);

        if (File.Exists(path) && await HashMatchesAsync(path, cancellationToken))
        {
            return path;
        }

        string temp = path + ".download";
        using (var http = new HttpClient())
        {
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Consolify (+https://github.com/TomSzo/Consolify)");
            await using var source = await http.GetStreamAsync(DownloadUrl, cancellationToken);
            await using var target = File.Create(temp);
            await source.CopyToAsync(target, cancellationToken);
        }

        if (!await HashMatchesAsync(temp, cancellationToken))
        {
            File.Delete(temp);
            throw new InvalidDataException(
                $"The downloaded PresentMon does not match the expected checksum. It was deleted and not run.");
        }

        File.Move(temp, path, overwrite: true);
        return path;
    }

    /// <summary>Whether this process runs with administrator rights.</summary>
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Records frames for a fixed time. PresentMon needs administrator rights to trace frames, so Windows
    /// shows a UAC prompt unless this process is already elevated.
    /// </summary>
    /// <param name="presentMonPath">Path from <see cref="EnsureAsync"/>.</param>
    /// <param name="outputCsv">Where to write the capture.</param>
    /// <param name="seconds">How long to record.</param>
    /// <param name="delaySeconds">Wait before recording, so the player can return to the game.</param>
    /// <param name="processName">Only record this executable, or null to record every process.</param>
    /// <exception cref="OperationCanceledException">The player declined the UAC prompt.</exception>
    public static void Capture(string presentMonPath, string outputCsv, int seconds, int delaySeconds, string? processName)
    {
        var args = new List<string>
        {
            "--output_file", Quote(outputCsv),
            "--timed", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--terminate_after_timed",
            "--delay", delaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--session_name", "Consolify",
            "--stop_existing_session",
            "--no_console_stats",
            "--track_frame_type",
        };

        if (!string.IsNullOrWhiteSpace(processName))
        {
            args.Add("--process_name");
            args.Add(Quote(processName));
        }

        var start = new ProcessStartInfo(presentMonPath, string.Join(' ', args))
        {
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            Verb = IsElevated() ? string.Empty : "runas",
        };

        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            throw new OperationCanceledException("The administrator prompt was declined, so nothing was recorded.", ex);
        }

        if (process is null)
        {
            throw new InvalidOperationException("PresentMon could not be started.");
        }

        using (process)
        {
            int timeoutMs = (seconds + delaySeconds + 60) * 1000;
            if (!process.WaitForExit(timeoutMs))
            {
                throw new TimeoutException("PresentMon did not finish in time.");
            }
        }

        if (!File.Exists(outputCsv))
        {
            throw new InvalidOperationException("PresentMon finished but wrote no capture file.");
        }
    }

    private static async Task<bool> HashMatchesAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).Equals(Sha256, StringComparison.OrdinalIgnoreCase);
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
