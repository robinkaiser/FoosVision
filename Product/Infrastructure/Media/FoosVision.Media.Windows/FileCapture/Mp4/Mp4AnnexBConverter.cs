// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Windows.FileCapture.Mp4;

internal class Mp4AnnexBConverter
{
    private static readonly byte[] _StartCode = [0x00, 0x00, 0x00, 0x01];

    private readonly CodecType _Codec;
    private readonly int _NalLengthSize;
    private readonly byte[] _ParameterSets;

    public Mp4AnnexBConverter(CodecType codec, ReadOnlySpan<byte> extraData)
    {
        _Codec = codec;
        _NalLengthSize = codec switch
        {
            CodecType.H264 => ParseH264(extraData, out _ParameterSets),
            CodecType.H265 => ParseH265(extraData, out _ParameterSets),
            _ => throw new NotSupportedException($"Codec '{codec}' is not supported for MP4 Annex-B conversion."),
        };
    }

    public byte[] ConvertPacket(ReadOnlySpan<byte> packetData, bool isKeyFrame)
    {
        if (packetData.IsEmpty)
        {
            return isKeyFrame && _ParameterSets.Length > 0 ? _ParameterSets.ToArray() : [];
        }

        if (IsAnnexB(packetData))
        {
            if (!isKeyFrame || _ParameterSets.Length == 0)
            {
                return packetData.ToArray();
            }

            byte[] prefixed = new byte[_ParameterSets.Length + packetData.Length];
            _ParameterSets.CopyTo(prefixed, 0);
            packetData.CopyTo(prefixed.AsSpan(_ParameterSets.Length));
            return prefixed;
        }

        int totalSize = isKeyFrame ? _ParameterSets.Length : 0;
        int offset = 0;
        while (offset + _NalLengthSize <= packetData.Length)
        {
            int nalSize = ReadNalLength(packetData.Slice(offset, _NalLengthSize));
            offset += _NalLengthSize;
            if (nalSize < 0 || offset + nalSize > packetData.Length)
            {
                throw new InvalidOperationException("MP4 packet contains an invalid NAL length.");
            }

            totalSize += _StartCode.Length + nalSize;
            offset += nalSize;
        }

        if (offset != packetData.Length)
        {
            throw new InvalidOperationException("MP4 packet contains trailing bytes after NAL parsing.");
        }

        byte[] result = new byte[totalSize];
        int destinationOffset = 0;
        if (isKeyFrame && _ParameterSets.Length > 0)
        {
            _ParameterSets.CopyTo(result, 0);
            destinationOffset += _ParameterSets.Length;
        }

        offset = 0;
        while (offset + _NalLengthSize <= packetData.Length)
        {
            int nalSize = ReadNalLength(packetData.Slice(offset, _NalLengthSize));
            offset += _NalLengthSize;
            _StartCode.CopyTo(result, destinationOffset);
            destinationOffset += _StartCode.Length;
            packetData.Slice(offset, nalSize).CopyTo(result.AsSpan(destinationOffset));
            destinationOffset += nalSize;
            offset += nalSize;
        }

        return result;
    }

    private static int ParseH264(ReadOnlySpan<byte> extraData, out byte[] parameterSets)
    {
        if (extraData.Length < 7)
        {
            parameterSets = [];
            return 4;
        }

        int nalLengthSize = (extraData[4] & 0x03) + 1;
        int spsCount = extraData[5] & 0x1F;
        int offset = 6;
        List<byte[]> units = [];

        for (int i = 0; i < spsCount; i++)
        {
            units.Add(ReadLengthPrefixedBlob(extraData, ref offset));
        }

        if (offset >= extraData.Length)
        {
            parameterSets = ComposeParameterSets(units);
            return nalLengthSize;
        }

        int ppsCount = extraData[offset++];
        for (int i = 0; i < ppsCount; i++)
        {
            units.Add(ReadLengthPrefixedBlob(extraData, ref offset));
        }

        parameterSets = ComposeParameterSets(units);
        return nalLengthSize;
    }

    private static int ParseH265(ReadOnlySpan<byte> extraData, out byte[] parameterSets)
    {
        if (extraData.Length < 23)
        {
            parameterSets = [];
            return 4;
        }

        int nalLengthSize = (extraData[21] & 0x03) + 1;
        int arrayCount = extraData[22];
        int offset = 23;
        List<byte[]> units = [];

        for (int arrayIndex = 0; arrayIndex < arrayCount; arrayIndex++)
        {
            if (offset + 3 > extraData.Length)
            {
                throw new InvalidOperationException("HEVC configuration record is truncated.");
            }

            offset += 1;
            int nalCount = ReadUInt16(extraData, ref offset);
            for (int nalIndex = 0; nalIndex < nalCount; nalIndex++)
            {
                units.Add(ReadLengthPrefixedBlob(extraData, ref offset));
            }
        }

        parameterSets = ComposeParameterSets(units);
        return nalLengthSize;
    }

    private static byte[] ReadLengthPrefixedBlob(ReadOnlySpan<byte> buffer, ref int offset)
    {
        int length = ReadUInt16(buffer, ref offset);
        if (length <= 0 || offset + length > buffer.Length)
        {
            throw new InvalidOperationException("Codec extradata contains an invalid parameter-set length.");
        }

        byte[] blob = buffer.Slice(offset, length).ToArray();
        offset += length;
        return blob;
    }

    private static int ReadUInt16(ReadOnlySpan<byte> buffer, ref int offset)
    {
        if (offset + 2 > buffer.Length)
        {
            throw new InvalidOperationException("Codec extradata is truncated.");
        }

        int value = (buffer[offset] << 8) | buffer[offset + 1];
        offset += 2;
        return value;
    }

    private static byte[] ComposeParameterSets(IReadOnlyList<byte[]> units)
    {
        if (units.Count == 0)
        {
            return [];
        }

        int totalSize = 0;
        foreach (byte[] unit in units)
        {
            totalSize += _StartCode.Length + unit.Length;
        }

        byte[] result = new byte[totalSize];
        int offset = 0;
        foreach (byte[] unit in units)
        {
            _StartCode.CopyTo(result, offset);
            offset += _StartCode.Length;
            unit.CopyTo(result, offset);
            offset += unit.Length;
        }

        return result;
    }

    private int ReadNalLength(ReadOnlySpan<byte> prefix)
    {
        int size = 0;
        for (int i = 0; i < _NalLengthSize; i++)
        {
            size = (size << 8) | prefix[i];
        }

        return size;
    }

    private static bool IsAnnexB(ReadOnlySpan<byte> buffer)
    {
        return buffer.Length >= 4 &&
               buffer[0] == 0x00 &&
               buffer[1] == 0x00 &&
               ((buffer[2] == 0x01) || (buffer[2] == 0x00 && buffer[3] == 0x01));
    }
}
