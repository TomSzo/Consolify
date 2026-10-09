// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Consolify.Windows.Display;

/// <summary>Reads (never writes) the per-user DirectX graphics settings that affect frame pacing.</summary>
[SupportedOSPlatform("windows")]
public static class WindowsGraphicsSettings
{
    private const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    private const string GlobalValueName = "DirectXUserGlobalSettings";

    /// <summary>
    /// The global settings string, e.g. <c>SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;</c>,
    /// parsed into key/value pairs. Empty when nothing has been set (Windows defaults apply).
    /// </summary>
    public static IReadOnlyDictionary<string, string> ReadGlobalSettings()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (key?.GetValue(GlobalValueName) is not string raw)
        {
            return new Dictionary<string, string>();
        }

        return raw
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Split('=', 2))
            .Where(pair => pair.Length == 2)
            .GroupBy(pair => pair[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last()[1], StringComparer.OrdinalIgnoreCase);
    }
}
