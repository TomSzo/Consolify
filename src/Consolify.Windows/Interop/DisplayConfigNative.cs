// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

using System.Runtime.InteropServices;

namespace Consolify.Windows.Interop;

/// <summary>P/Invoke declarations for the Win32 display configuration API (wingdi.h).</summary>
internal static partial class DisplayConfigNative
{
    public const uint QdcOnlyActivePaths = 0x00000002;
    public const int ErrorSuccess = 0;
    public const int ErrorInsufficientBuffer = 122;

    public const int ModeInfoTypeSource = 1;
    public const int ModeInfoTypeTarget = 2;

    public const int DeviceInfoGetTargetName = 2;

    public const uint OutputTechnologyInternal = 0x80000000;
    public const int OutputTechnologyHdmi = 5;
    public const int OutputTechnologyLvds = 6;
    public const int OutputTechnologyDisplayPortExternal = 10;
    public const int OutputTechnologyDisplayPortEmbedded = 11;
    public const int OutputTechnologyUdiEmbedded = 13;
    public const int OutputTechnologyDisplayPortUsbTunnel = 18;

    [StructLayout(LayoutKind.Sequential)]
    public struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rational
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PathSourceInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint StatusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PathTargetInfo
    {
        public Luid AdapterId;
        public uint Id;
        public uint ModeInfoIdx;
        public uint OutputTechnology;
        public int Rotation;
        public int Scaling;
        public Rational RefreshRate;
        public int ScanLineOrdering;
        public int TargetAvailable;
        public uint StatusFlags;
    }

    /// <summary>DISPLAYCONFIG_PATH_INFO, 72 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PathInfo
    {
        public PathSourceInfo SourceInfo;
        public PathTargetInfo TargetInfo;
        public uint Flags;
    }

    /// <summary>DISPLAYCONFIG_MODE_INFO, 64 bytes; the mode union starts at offset 16.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModeInfo
    {
        [FieldOffset(0)] public int InfoType;
        [FieldOffset(4)] public uint Id;
        [FieldOffset(8)] public Luid AdapterId;

        // DISPLAYCONFIG_SOURCE_MODE
        [FieldOffset(16)] public uint SourceWidth;
        [FieldOffset(20)] public uint SourceHeight;

        // DISPLAYCONFIG_TARGET_MODE.targetVideoSignalInfo
        [FieldOffset(32)] public Rational TargetVSyncFrequency;
        [FieldOffset(40)] public uint TargetActiveWidth;
        [FieldOffset(44)] public uint TargetActiveHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DeviceInfoHeader
    {
        public int Type;
        public uint Size;
        public Luid AdapterId;
        public uint Id;
    }

    /// <summary>DISPLAYCONFIG_TARGET_DEVICE_NAME, 420 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TargetDeviceName
    {
        public DeviceInfoHeader Header;
        public uint Flags;
        public uint OutputTechnology;
        public ushort EdidManufactureId;
        public ushort EdidProductCodeId;
        public uint ConnectorInstance;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string MonitorFriendlyDeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string MonitorDevicePath;
    }

    [DllImport("user32.dll")]
    public static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    public static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] PathInfo[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] ModeInfo[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll")]
    public static extern int DisplayConfigGetDeviceInfo(ref TargetDeviceName requestPacket);
}
