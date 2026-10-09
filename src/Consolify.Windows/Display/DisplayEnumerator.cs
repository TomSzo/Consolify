// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Consolify.Core.Display;
using Consolify.Windows.Interop;
using Microsoft.Win32;
using static Consolify.Windows.Interop.DisplayConfigNative;

namespace Consolify.Windows.Display;

/// <summary>Lists the active displays and reads each one's EDID. Read-only: changes nothing.</summary>
[SupportedOSPlatform("windows")]
public static class DisplayEnumerator
{
    /// <summary>Returns every display that is currently part of the desktop.</summary>
    public static IReadOnlyList<ConnectedDisplay> GetActiveDisplays()
    {
        var (paths, modes) = QueryActivePaths();
        var displays = new List<ConnectedDisplay>(paths.Length);

        foreach (var path in paths)
        {
            var target = GetTargetName(path.TargetInfo);
            var (width, height) = GetSourceResolution(path.SourceInfo, modes);
            double refresh = path.TargetInfo.RefreshRate.Denominator == 0
                ? 0
                : (double)path.TargetInfo.RefreshRate.Numerator / path.TargetInfo.RefreshRate.Denominator;

            var (edid, edidError) = ReadEdid(target.MonitorDevicePath);
            var connection = MapConnection(path.TargetInfo.OutputTechnology);

            displays.Add(new ConnectedDisplay
            {
                FriendlyName = ChooseName(target.MonitorFriendlyDeviceName, edid, connection),
                DevicePath = target.MonitorDevicePath,
                Connection = connection,
                OutputTechnologyRaw = path.TargetInfo.OutputTechnology,
                Width = width,
                Height = height,
                RefreshHz = Math.Round(refresh, 3),
                Edid = edid,
                EdidError = edidError,
            });
        }

        return displays;
    }

    private static (PathInfo[] Paths, ModeInfo[] Modes) QueryActivePaths()
    {
        // The topology can change between the two calls, so retry when the buffers turn out too small.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int status = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount);
            if (status != ErrorSuccess)
            {
                throw new InvalidOperationException($"GetDisplayConfigBufferSizes failed with error {status}.");
            }

            var paths = new PathInfo[pathCount];
            var modes = new ModeInfo[modeCount];
            status = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);

            if (status == ErrorSuccess)
            {
                return (paths[..(int)pathCount], modes[..(int)modeCount]);
            }

            if (status != ErrorInsufficientBuffer)
            {
                throw new InvalidOperationException($"QueryDisplayConfig failed with error {status}.");
            }
        }

        throw new InvalidOperationException("The display configuration kept changing; try again.");
    }

    private static TargetDeviceName GetTargetName(PathTargetInfo target)
    {
        var request = new TargetDeviceName
        {
            Header = new DeviceInfoHeader
            {
                Type = DeviceInfoGetTargetName,
                Size = (uint)Marshal.SizeOf<TargetDeviceName>(),
                AdapterId = target.AdapterId,
                Id = target.Id,
            },
            MonitorFriendlyDeviceName = string.Empty,
            MonitorDevicePath = string.Empty,
        };

        int status = DisplayConfigGetDeviceInfo(ref request);
        return status == ErrorSuccess
            ? request
            : request with { MonitorFriendlyDeviceName = string.Empty, MonitorDevicePath = string.Empty };
    }

    private static (int Width, int Height) GetSourceResolution(PathSourceInfo source, ModeInfo[] modes)
    {
        if (source.ModeInfoIdx < modes.Length && modes[source.ModeInfoIdx].InfoType == ModeInfoTypeSource)
        {
            var mode = modes[source.ModeInfoIdx];
            return ((int)mode.SourceWidth, (int)mode.SourceHeight);
        }

        return (0, 0);
    }

    /// <summary>
    /// Maps a monitor device path such as <c>\\?\DISPLAY#AUO82ED#4&amp;1a2b&amp;0&amp;UID8388688#{guid}</c>
    /// to its registry key and reads the EDID stored there.
    /// </summary>
    private static (EdidInfo? Edid, string? Error) ReadEdid(string devicePath)
    {
        if (string.IsNullOrEmpty(devicePath))
        {
            return (null, "Windows did not report a device path for this display.");
        }

        var parts = devicePath.Replace(@"\\?\", string.Empty, StringComparison.Ordinal).Split('#');
        if (parts.Length < 3)
        {
            return (null, $"Unexpected device path format: {devicePath}");
        }

        string keyPath = $@"SYSTEM\CurrentControlSet\Enum\{parts[0]}\{parts[1]}\{parts[2]}\Device Parameters";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key?.GetValue("EDID") is not byte[] raw)
            {
                return (null, "No EDID stored for this display.");
            }

            return (EdidParser.Parse(raw), null);
        }
        catch (EdidFormatException ex)
        {
            return (null, ex.Message);
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return (null, $"Could not read the EDID: {ex.Message}");
        }
    }

    /// <summary>
    /// Windows often reports no friendly name for built-in panels, so fall back to the EDID name and then
    /// to the manufacturer and product code.
    /// </summary>
    private static string ChooseName(string? windowsName, EdidInfo? edid, DisplayConnection connection)
    {
        if (!string.IsNullOrWhiteSpace(windowsName))
        {
            return windowsName;
        }

        if (!string.IsNullOrWhiteSpace(edid?.MonitorName))
        {
            return edid.MonitorName;
        }

        string prefix = connection == DisplayConnection.Internal ? "Built-in display" : "Display";
        return edid is null ? prefix : $"{prefix} ({edid.ManufacturerId} 0x{edid.ProductCode:X4})";
    }

    private static DisplayConnection MapConnection(uint outputTechnology) => outputTechnology switch
    {
        OutputTechnologyInternal => DisplayConnection.Internal,
        OutputTechnologyLvds or OutputTechnologyDisplayPortEmbedded or OutputTechnologyUdiEmbedded => DisplayConnection.Internal,
        OutputTechnologyHdmi => DisplayConnection.Hdmi,
        OutputTechnologyDisplayPortExternal => DisplayConnection.DisplayPort,
        OutputTechnologyDisplayPortUsbTunnel => DisplayConnection.UsbC,
        uint.MaxValue => DisplayConnection.Unknown,
        _ => DisplayConnection.Other,
    };
}
