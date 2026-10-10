// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Text.Json;

namespace Consolify.Core.Viewing;

/// <summary>What the player told Consolify about one display. Null means "not told".</summary>
/// <param name="DiagonalInches">The real screen size.</param>
/// <param name="DistanceCm">How far the player sits from it.</param>
public sealed record DisplaySettings(double? DiagonalInches = null, double? DistanceCm = null);

/// <summary>Where a size or distance came from, so the report can say how much to trust it.</summary>
public enum ValueSource
{
    /// <summary>Not known at all.</summary>
    Unknown,

    /// <summary>A built-in assumption.</summary>
    Default,

    /// <summary>Read from the display's EDID.</summary>
    Edid,

    /// <summary>Told by the player.</summary>
    Player,
}

/// <summary>Remembers what the player told about each display, in a small JSON file.</summary>
public sealed class DisplaySettingsStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Dictionary<string, DisplaySettings> _displays;

    public DisplaySettingsStore(string path)
    {
        _path = path;
        _displays = File.Exists(path)
            ? JsonSerializer.Deserialize<Dictionary<string, DisplaySettings>>(File.ReadAllText(path), Json) ?? []
            : [];
    }

    /// <summary>The key of a display model: EDID manufacturer and product code, e.g. "GSM82CD".</summary>
    public static string KeyFor(string manufacturerId, ushort productCode) => $"{manufacturerId}{productCode:X4}";

    public DisplaySettings Get(string key) => _displays.GetValueOrDefault(key) ?? new DisplaySettings();

    /// <summary>Stores the given values; values left null keep what was stored before.</summary>
    public void Set(string key, DisplaySettings settings)
    {
        var old = Get(key);
        _displays[key] = new DisplaySettings(
            settings.DiagonalInches ?? old.DiagonalInches,
            settings.DistanceCm ?? old.DistanceCm);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_displays, Json));
    }

    /// <summary>
    /// Whether the EDID's physical size can be used without asking. TVs often report a placeholder
    /// (16:9 at 160x90 cm is common) or nothing, so for TVs the player's answer is needed.
    /// </summary>
    public static bool EdidSizeTrusted(DisplayKind kind) => kind != DisplayKind.Television;
}
