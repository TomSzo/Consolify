// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Consolify contributors

namespace Consolify.Core.Tests.Display;

/// <summary>Builds small synthetic EDIDs for tests.</summary>
internal sealed class EdidBuilder
{
    private readonly byte[] _base = new byte[128];
    private readonly List<byte[]> _extensions = [];
    private int _nextDescriptor;

    public EdidBuilder(string manufacturer = "SAM", ushort productCode = 0x1234, byte minorVersion = 4)
    {
        byte[] header = [0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00];
        header.CopyTo(_base, 0);

        int code = ((manufacturer[0] - 'A' + 1) << 10) | ((manufacturer[1] - 'A' + 1) << 5) | (manufacturer[2] - 'A' + 1);
        _base[8] = (byte)(code >> 8);
        _base[9] = (byte)code;
        _base[10] = (byte)productCode;
        _base[11] = (byte)(productCode >> 8);
        _base[18] = 1;
        _base[19] = minorVersion;
    }

    public EdidBuilder WithSize(byte widthCm, byte heightCm)
    {
        _base[21] = widthCm;
        _base[22] = heightCm;
        return this;
    }

    public EdidBuilder WithContinuousFrequency()
    {
        _base[24] |= 0x01;
        return this;
    }

    public EdidBuilder WithName(string name)
    {
        var d = NextDescriptor(0xFC);
        for (int i = 0; i < 13; i++)
        {
            d[5 + i] = i < name.Length ? (byte)name[i] : i == name.Length ? (byte)0x0A : (byte)0x20;
        }

        return this;
    }

    public EdidBuilder WithRangeLimits(int minHz, int maxHz, byte flags = 0x01)
    {
        var d = NextDescriptor(0xFD);
        byte offsets = 0;
        if (minHz > 255) { offsets |= 0x01; minHz -= 255; }
        if (maxHz > 255) { offsets |= 0x02; maxHz -= 255; }
        d[4] = offsets;
        d[5] = (byte)minHz;
        d[6] = (byte)maxHz;
        d[10] = flags;
        return this;
    }

    /// <summary>Adds a CTA-861 extension holding the given data blocks (each already with its header byte).</summary>
    public EdidBuilder WithCtaExtension(params byte[][] dataBlocks)
    {
        var block = new byte[128];
        block[0] = 0x02;
        block[1] = 0x03;
        int position = 4;
        foreach (var dataBlock in dataBlocks)
        {
            dataBlock.CopyTo(block, position);
            position += dataBlock.Length;
        }

        block[2] = (byte)position;
        _extensions.Add(block);
        return this;
    }

    public static byte[] HdmiLlcBlock() => VendorBlock(0x000C03, [0x10, 0x00]);

    public static byte[] HdmiForumBlock(int vrrMin, int vrrMax, bool allm = true) =>
        VendorBlock(0xC45DD8,
        [
            0x01, 0x78, 0x00, 0x00,
            (byte)(allm ? 0x02 : 0x00),
            (byte)((vrrMin & 0x3F) | ((vrrMax >> 8) << 6)),
            (byte)(vrrMax & 0xFF),
        ]);

    public static byte[] AmdBlock(int version, int minHz, int maxHz)
    {
        if (version < 3)
        {
            return VendorBlock(0x00001A, [(byte)version, 0x01, (byte)minHz, (byte)maxHz, 0x00]);
        }

        var body = new byte[12];
        body[0] = (byte)version;
        body[1] = 0x01;
        body[2] = (byte)minHz;
        body[3] = 0x00;
        body[10] = (byte)(maxHz & 0xFF);
        body[11] = (byte)((maxHz >> 8) & 0x03);
        return VendorBlock(0x00001A, body);
    }

    public byte[] Build()
    {
        _base[126] = (byte)_extensions.Count;
        FixChecksum(_base);
        var result = new List<byte>(_base);
        foreach (var extension in _extensions)
        {
            FixChecksum(extension);
            result.AddRange(extension);
        }

        return [.. result];
    }

    private static byte[] VendorBlock(int oui, byte[] body)
    {
        var block = new byte[1 + 3 + body.Length];
        block[0] = (byte)((3 << 5) | (3 + body.Length));
        block[1] = (byte)oui;
        block[2] = (byte)(oui >> 8);
        block[3] = (byte)(oui >> 16);
        body.CopyTo(block, 4);
        return block;
    }

    private Span<byte> NextDescriptor(byte tag)
    {
        var d = _base.AsSpan(54 + (18 * _nextDescriptor++), 18);
        d.Clear();
        d[3] = tag;
        return d;
    }

    private static void FixChecksum(byte[] block)
    {
        int sum = 0;
        for (int i = 0; i < 127; i++)
        {
            sum += block[i];
        }

        block[127] = (byte)((256 - (sum & 0xFF)) & 0xFF);
    }
}
