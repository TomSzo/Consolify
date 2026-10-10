// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using Consolify.Core.Applying;
using Consolify.Windows.Measurement;

namespace Consolify.Windows.Applying;

/// <summary>A backup made before a change, and where it belongs.</summary>
/// <param name="BackupPath">The saved copy.</param>
/// <param name="OriginalPath">The file it was copied from, and where a restore puts it back.</param>
public sealed record Backup(string BackupPath, string OriginalPath);

/// <summary>
/// Sets the frame cap through OptiScaler's own limiter (<c>[Framerate] FramerateLimit</c> in OptiScaler.ini).
/// Every change is preceded by a backup, and <see cref="Restore"/> puts the last one back.
/// </summary>
/// <remarks>OptiScaler reads its ini when the game starts, so a new cap applies from the next launch.</remarks>
[SupportedOSPlatform("windows")]
public static class OptiScalerFrameCap
{
    public const string IniName = "OptiScaler.ini";
    private const string Section = "Framerate";
    private const string Key = "FramerateLimit";
    private const string OriginFile = "original-path.txt";

    /// <summary>Where backups of one game's files are kept.</summary>
    public static string BackupDirectory(string gameExeName) =>
        Path.Combine(PresentMonRunner.DataDirectory, "backups", Path.GetFileNameWithoutExtension(gameExeName));

    /// <summary>The OptiScaler.ini next to the game's executable, or null if OptiScaler is not installed there.</summary>
    public static string? FindIni(string gameExePath)
    {
        string? folder = Path.GetDirectoryName(gameExePath);
        string? ini = folder is null ? null : Path.Combine(folder, IniName);
        return ini is not null && File.Exists(ini) ? ini : null;
    }

    /// <summary>The cap currently in the ini, or null if none is set ("auto" or 0 means none).</summary>
    public static double? ReadCap(string iniPath)
    {
        string? value = IniText.GetValue(File.ReadAllText(iniPath), Section, Key);
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double fps) && fps > 0
            ? fps
            : null;
    }

    /// <summary>Backs up the ini, then writes the cap. Returns the backup.</summary>
    /// <exception cref="UnauthorizedAccessException">The game folder is not writable without admin rights.</exception>
    public static Backup Apply(string iniPath, int capFps, string gameExeName)
    {
        var backup = MakeBackup(iniPath, gameExeName);

        byte[] bytes = File.ReadAllBytes(iniPath);
        bool bom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        string text = new UTF8Encoding(false).GetString(bytes, bom ? 3 : 0, bytes.Length - (bom ? 3 : 0));

        string updated = IniText.SetValue(text, Section, Key, capFps.ToString(CultureInfo.InvariantCulture));
        File.WriteAllText(iniPath, updated, new UTF8Encoding(bom));
        return backup;
    }

    /// <summary>Puts the newest backup of this game back. Returns it, or null if there is none.</summary>
    public static Backup? Restore(string gameExeName)
    {
        string folder = BackupDirectory(gameExeName);
        if (!Directory.Exists(folder))
        {
            return null;
        }

        string? latest = Directory.GetDirectories(folder).OrderDescending(StringComparer.Ordinal).FirstOrDefault();
        if (latest is null || !File.Exists(Path.Combine(latest, OriginFile)))
        {
            return null;
        }

        string original = File.ReadAllText(Path.Combine(latest, OriginFile)).Trim();
        string copy = Path.Combine(latest, Path.GetFileName(original));
        File.Copy(copy, original, overwrite: true);
        Directory.Move(latest, latest + "-restored");
        return new Backup(copy, original);
    }

    private static Backup MakeBackup(string path, string gameExeName)
    {
        string folder = Path.Combine(BackupDirectory(gameExeName), DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        string copy = Path.Combine(folder, Path.GetFileName(path));
        File.Copy(path, copy, overwrite: true);
        File.WriteAllText(Path.Combine(folder, OriginFile), path);
        return new Backup(copy, path);
    }
}
