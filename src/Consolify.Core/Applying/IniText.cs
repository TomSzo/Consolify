// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Text.RegularExpressions;

namespace Consolify.Core.Applying;

/// <summary>
/// Reads and changes single values in INI text without touching anything else: comments, blank lines,
/// spacing around '=', line endings and the order of keys all stay as they were.
/// </summary>
public static partial class IniText
{
    /// <summary>The value of <paramref name="key"/> in <paramref name="section"/>, or null if it is not set.</summary>
    public static string? GetValue(string text, string section, string key)
    {
        foreach (var line in Lines(text))
        {
            if (line.Section is not null && line.Section.Equals(section, StringComparison.OrdinalIgnoreCase)
                && line.Key is not null && line.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return line.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Sets <paramref name="key"/> in <paramref name="section"/>. An existing line keeps its spacing;
    /// a missing key is added right after the section header; a missing section is added at the end.
    /// </summary>
    public static string SetValue(string text, string section, string key, string value)
    {
        string newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var parts = text.Split('\n');
        string? current = null;
        int header = -1;

        for (int i = 0; i < parts.Length; i++)
        {
            string raw = parts[i].TrimEnd('\r');
            var sectionMatch = SectionRegex().Match(raw);
            if (sectionMatch.Success)
            {
                current = sectionMatch.Groups[1].Value.Trim();
                if (current.Equals(section, StringComparison.OrdinalIgnoreCase))
                {
                    header = i;
                }

                continue;
            }

            if (current is null || !current.Equals(section, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var keyMatch = KeyRegex().Match(raw);
            if (keyMatch.Success && keyMatch.Groups["key"].Value.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                bool cr = parts[i].EndsWith('\r');
                parts[i] = keyMatch.Groups["prefix"].Value + value + (cr ? "\r" : string.Empty);
                return string.Join('\n', parts);
            }
        }

        if (header >= 0)
        {
            bool cr = parts[header].EndsWith('\r');
            var list = parts.ToList();
            list.Insert(header + 1, $"{key}={value}" + (cr ? "\r" : string.Empty));
            return string.Join('\n', list);
        }

        string tail = text.Length == 0 || text.EndsWith('\n') ? string.Empty : newline;
        return text + tail + $"[{section}]{newline}{key}={value}{newline}";
    }

    private static IEnumerable<(string? Section, string? Key, string? Value)> Lines(string text)
    {
        string? section = null;
        foreach (var part in text.Split('\n'))
        {
            string raw = part.TrimEnd('\r');
            var s = SectionRegex().Match(raw);
            if (s.Success)
            {
                section = s.Groups[1].Value.Trim();
                continue;
            }

            var k = KeyRegex().Match(raw);
            yield return k.Success
                ? (section, k.Groups["key"].Value, k.Groups["value"].Value.Trim())
                : (section, null, null);
        }
    }

    [GeneratedRegex(@"^\s*\[([^\]]+)\]\s*$")]
    private static partial Regex SectionRegex();

    [GeneratedRegex(@"^(?<prefix>\s*(?<key>[^;#=\s][^=]*?)\s*=\s*)(?<value>.*)$")]
    private static partial Regex KeyRegex();
}
