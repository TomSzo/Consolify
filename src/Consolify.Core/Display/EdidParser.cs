// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Display;

/// <summary>Thrown when data is not a usable EDID.</summary>
public sealed class EdidFormatException(string message) : FormatException(message);

/// <summary>
/// Parses the parts of an EDID that matter for Consolify: identity, physical size and every
/// advertised variable refresh rate range.
/// </summary>
/// <remarks>
/// Field layouts follow VESA E-EDID 1.4, CTA-861 and the HDMI 2.1 Forum blocks, cross-checked against
/// the Linux kernel (drivers/gpu/drm/drm_edid.c) and edid-decode (utils/edid-decode/parse-cta-block.cpp).
/// DisplayID extensions are not parsed yet.
/// </remarks>
public static class EdidParser
{
    private const int BlockSize = 128;

    private static readonly byte[] Header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];

    // IEEE OUIs, as numbers. In CTA-861 vendor blocks they are stored little-endian.
    private const int OuiHdmiLlc = 0x000C03;
    private const int OuiHdmiForum = 0xC45DD8;
    private const int OuiAmd = 0x00001A;

    private const byte TagCtaExtension = 0x02;
    private const byte TagDisplayIdExtension = 0x70;

    private const byte DescriptorMonitorName = 0xFC;
    private const byte DescriptorRangeLimits = 0xFD;
    private const byte RangeLimitsOnlyFlag = 0x01;

    private const byte CtaTagVendorSpecific = 3;
    private const byte CtaTagExtended = 7;
    private const byte CtaExtendedTagHdmiForumScdb = 0x79;

    /// <summary>Parses a raw EDID (128 bytes or a multiple of it).</summary>
    /// <exception cref="EdidFormatException">The data is too short or the EDID header is missing.</exception>
    public static EdidInfo Parse(ReadOnlySpan<byte> edid)
    {
        if (edid.Length < BlockSize)
        {
            throw new EdidFormatException($"EDID is {edid.Length} bytes; at least {BlockSize} are required.");
        }

        if (!edid[..Header.Length].SequenceEqual(Header))
        {
            throw new EdidFormatException("Missing EDID header (00 FF FF FF FF FF FF 00).");
        }

        var warnings = new List<string>();
        var ranges = new List<VrrRange>();
        var baseBlock = edid[..BlockSize];

        if (!ChecksumValid(baseBlock))
        {
            warnings.Add("Base block checksum is invalid.");
        }

        var version = new Version(baseBlock[18], baseBlock[19]);
        bool isEdid14 = version >= new Version(1, 4);
        bool continuousFrequency = isEdid14 && (baseBlock[24] & 0x01) != 0;
        string? monitorName = null;

        for (int i = 0; i < 4; i++)
        {
            var descriptor = baseBlock.Slice(54 + (18 * i), 18);
            bool isDisplayDescriptor = descriptor[0] == 0 && descriptor[1] == 0 && descriptor[2] == 0;
            if (!isDisplayDescriptor)
            {
                continue;
            }

            switch (descriptor[3])
            {
                case DescriptorMonitorName:
                    monitorName = ReadDescriptorText(descriptor[5..]);
                    break;

                // Like the Linux kernel, trust the range descriptor for VRR only on EDID 1.4 displays that
                // declare continuous frequency and use the "range limits only" variant.
                case DescriptorRangeLimits when continuousFrequency && descriptor[10] == RangeLimitsOnlyFlag:
                    int min = descriptor[5] + ((descriptor[4] & 0x01) != 0 ? 255 : 0);
                    int max = descriptor[6] + ((descriptor[4] & 0x02) != 0 ? 255 : 0);
                    ranges.Add(new VrrRange(min, max, VrrSource.MonitorRangeLimits));
                    break;
            }
        }

        bool hasHdmiBlock = false;
        bool supportsAllm = false;

        int extensionCount = baseBlock[126];
        int availableExtensions = (edid.Length / BlockSize) - 1;
        if (extensionCount > availableExtensions)
        {
            warnings.Add($"EDID declares {extensionCount} extension block(s) but only {availableExtensions} are present.");
            extensionCount = availableExtensions;
        }

        for (int block = 1; block <= extensionCount; block++)
        {
            var extension = edid.Slice(block * BlockSize, BlockSize);
            if (!ChecksumValid(extension))
            {
                warnings.Add($"Extension block {block} checksum is invalid.");
            }

            switch (extension[0])
            {
                case TagCtaExtension:
                    ParseCtaExtension(extension, ranges, warnings, ref hasHdmiBlock, ref supportsAllm);
                    break;
                case TagDisplayIdExtension:
                    warnings.Add($"Extension block {block} is DisplayID, which is not parsed yet.");
                    break;
                default:
                    warnings.Add($"Extension block {block} has unsupported tag 0x{extension[0]:X2}.");
                    break;
            }
        }

        int manufacturerCode = (baseBlock[8] << 8) | baseBlock[9];

        return new EdidInfo
        {
            ManufacturerId = DecodeManufacturer(manufacturerCode),
            ProductCode = (ushort)(baseBlock[10] | (baseBlock[11] << 8)),
            MonitorName = monitorName,
            EdidVersion = version,
            WidthCm = baseBlock[21],
            HeightCm = baseBlock[22],
            ContinuousFrequency = continuousFrequency,
            HasHdmiBlock = hasHdmiBlock,
            SupportsAllm = supportsAllm,
            VrrRanges = ranges,
            BlockCount = extensionCount + 1,
            Warnings = warnings,
        };
    }

    private static void ParseCtaExtension(
        ReadOnlySpan<byte> block,
        List<VrrRange> ranges,
        List<string> warnings,
        ref bool hasHdmiBlock,
        ref bool supportsAllm)
    {
        // Byte 2 is the offset of the first detailed timing; data blocks live between byte 4 and it.
        int end = block[2];
        if (end is 0 or < 4)
        {
            return;
        }

        end = Math.Min(end, BlockSize - 1);
        int position = 4;

        while (position < end)
        {
            int tag = block[position] >> 5;
            int length = block[position] & 0x1F;
            if (position + 1 + length > end)
            {
                warnings.Add("A CTA-861 data block runs past the data block collection.");
                return;
            }

            var payload = block.Slice(position + 1, length);
            position += 1 + length;

            if (tag == CtaTagVendorSpecific && payload.Length >= 3)
            {
                int oui = payload[0] | (payload[1] << 8) | (payload[2] << 16);
                var body = payload[3..];
                switch (oui)
                {
                    case OuiHdmiLlc:
                        hasHdmiBlock = true;
                        break;
                    case OuiHdmiForum:
                        ParseHdmiForum(body, ranges, ref supportsAllm);
                        break;
                    case OuiAmd:
                        ParseAmd(body, ranges);
                        break;
                }
            }
            else if (tag == CtaTagExtended && payload.Length >= 3 && payload[0] == CtaExtendedTagHdmiForumScdb)
            {
                // HF-SCDB: extended tag, two reserved bytes, then the same layout as the HF-VSDB body.
                ParseHdmiForum(payload[3..], ranges, ref supportsAllm);
            }
        }
    }

    /// <summary>HDMI Forum VSDB/SCDB body (after the OUI): byte 4 flags, bytes 5-6 VRRmin/VRRmax.</summary>
    private static void ParseHdmiForum(ReadOnlySpan<byte> body, List<VrrRange> ranges, ref bool supportsAllm)
    {
        if (body.Length > 4 && (body[4] & 0x02) != 0)
        {
            supportsAllm = true;
        }

        if (body.Length > 6)
        {
            int min = body[5] & 0x3F;
            int max = ((body[5] & 0xC0) << 2) | body[6];
            if (min > 0)
            {
                // A zero VRRmax means "up to the current video format's refresh rate".
                ranges.Add(new VrrRange(min, max, VrrSource.HdmiForumVrr));
            }
        }
    }

    /// <summary>
    /// AMD VSDB body (after the OUI): byte 0 version, byte 1 bit 0 FreeSync, byte 2 minimum refresh,
    /// byte 3 maximum refresh (version 1-2) or bytes 10-11 for version 3 and later.
    /// </summary>
    private static void ParseAmd(ReadOnlySpan<byte> body, List<VrrRange> ranges)
    {
        if (body.Length < 4 || (body[1] & 0x01) == 0)
        {
            return;
        }

        int version = body[0];
        int min = body[2];
        int max = version >= 3 && body.Length >= 12
            ? body[10] | ((body[11] & 0x03) << 8)
            : body[3];

        ranges.Add(new VrrRange(min, max, VrrSource.AmdFreeSync));
    }

    private static string DecodeManufacturer(int code)
    {
        char Letter(int value) => value is >= 1 and <= 26 ? (char)('A' + value - 1) : '?';
        return string.Concat(Letter((code >> 10) & 0x1F), Letter((code >> 5) & 0x1F), Letter(code & 0x1F));
    }

    private static string? ReadDescriptorText(ReadOnlySpan<byte> text)
    {
        var characters = new List<char>(text.Length);
        foreach (byte b in text)
        {
            if (b == 0x0A)
            {
                break;
            }

            characters.Add(b is >= 0x20 and < 0x7F ? (char)b : '?');
        }

        string result = new string(characters.ToArray()).Trim();
        return result.Length > 0 ? result : null;
    }

    private static bool ChecksumValid(ReadOnlySpan<byte> block)
    {
        int sum = 0;
        foreach (byte b in block)
        {
            sum += b;
        }

        return (sum & 0xFF) == 0;
    }
}
