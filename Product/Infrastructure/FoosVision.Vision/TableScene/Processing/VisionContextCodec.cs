// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;
using FoosVision.Vision.Common;

namespace FoosVision.Vision.TableScene.Processing;

public static unsafe class VisionContextCodec
{
    public const int QuantizedColorCount = 1 << 18;
    public const int MaxPaletteCount = 1 << 15;

    private const uint _Magic = 0x32435646; // FVC2
    private const int _HeaderLength = 44;
    private const int _TeamAModelOffset = 10;
    private const int _TeamBModelOffset = 26;
    private const int _PaletteCountOffset = 42;
    private const byte _HasTeamAFlag = 1;
    private const byte _HasTeamBFlag = 2;

    public static int GetMaxEncodedLength(int pixelCount)
    {
        int paletteCount = Math.Min(pixelCount, MaxPaletteCount);

        return _HeaderLength + (paletteCount * 3) + (pixelCount * 3);
    }

    public static bool TryEncode(
        int width,
        int height,
        byte[] inColorResponse32bpp,
        PlayerColorExclusionContext playerColorExclusion,
        byte[] outEncoded,
        int[] valueCounts,
        int[] paletteValues,
        ushort[] valueIndices,
        out int encodedLength)
    {
        int pixelCount = width * height;

        if (inColorResponse32bpp.Length < pixelCount * 4 ||
            valueCounts.Length < QuantizedColorCount ||
            paletteValues.Length < QuantizedColorCount ||
            valueIndices.Length < QuantizedColorCount)
        {
            encodedLength = 0;
            return false;
        }

        fixed (byte* pInColorResponse32bpp = inColorResponse32bpp)
        fixed (byte* pOutEncoded = outEncoded)
        fixed (int* pValueCounts = valueCounts)
        fixed (int* pPaletteValues = paletteValues)
        fixed (ushort* pValueIndices = valueIndices)
        {
            return TryEncode(width, height, pInColorResponse32bpp, playerColorExclusion, outEncoded.Length,
                pOutEncoded, pValueCounts, pPaletteValues, pValueIndices, out encodedLength);
        }
    }

    public static bool TryDecode(
        byte[] inEncoded,
        int encodedLength,
        byte[] outColorResponse32bpp,
        int[] paletteValues,
        out PlayerColorExclusionContext playerColorExclusion)
    {
        if (encodedLength > inEncoded.Length)
        {
            playerColorExclusion = default;
            return false;
        }

        fixed (byte* pInEncoded = inEncoded)
        fixed (byte* pOutColorResponse32bpp = outColorResponse32bpp)
        fixed (int* pPaletteValues = paletteValues)
        {
            return TryDecode(pInEncoded, encodedLength, pOutColorResponse32bpp,
                outColorResponse32bpp.Length / 4, pPaletteValues, paletteValues.Length, out playerColorExclusion);
        }
    }

    private static bool TryEncode(
        int width,
        int height,
        byte* pInColorResponse32bpp,
        PlayerColorExclusionContext playerColorExclusion,
        int outEncodedCapacity,
        byte* pOutEncoded,
        int* pValueCounts,
        int* pPaletteValues,
        ushort* pValueIndices,
        out int encodedLength)
    {
        int pixelCount = width * height;
        int paletteCount = CountPaletteValues(pixelCount, pInColorResponse32bpp, pValueCounts, pPaletteValues);

        if (paletteCount > MaxPaletteCount)
        {
            ClearPaletteState(pValueCounts, pPaletteValues, pValueIndices, paletteCount);
            encodedLength = 0;
            return false;
        }

        SortPaletteValuesByFrequency(pPaletteValues, pValueCounts, 0, paletteCount - 1);

        int requiredHeaderLength = _HeaderLength + (paletteCount * 3);

        if (outEncodedCapacity < requiredHeaderLength)
        {
            ClearPaletteState(pValueCounts, pPaletteValues, pValueIndices, paletteCount);
            encodedLength = 0;
            return false;
        }

        uint magic = _Magic;
        pOutEncoded[0] = (byte)magic;
        pOutEncoded[1] = (byte)(magic >> 8);
        pOutEncoded[2] = (byte)(magic >> 16);
        pOutEncoded[3] = (byte)(magic >> 24);
        pOutEncoded[4] = (byte)pixelCount;
        pOutEncoded[5] = (byte)(pixelCount >> 8);
        pOutEncoded[6] = (byte)(pixelCount >> 16);
        pOutEncoded[7] = (byte)(pixelCount >> 24);
        pOutEncoded[8] = CreatePlayerColorExclusionFlags(playerColorExclusion);
        pOutEncoded[9] = 0;
        WriteColorModel(pOutEncoded + _TeamAModelOffset, playerColorExclusion.HasTeamA, playerColorExclusion.TeamA);
        WriteColorModel(pOutEncoded + _TeamBModelOffset, playerColorExclusion.HasTeamB, playerColorExclusion.TeamB);
        pOutEncoded[_PaletteCountOffset] = (byte)paletteCount;
        pOutEncoded[_PaletteCountOffset + 1] = (byte)(paletteCount >> 8);

        byte* pOut = pOutEncoded + _HeaderLength;

        for (int i = 0; i < paletteCount; i++)
        {
            int value = pPaletteValues[i];
            pValueIndices[value] = (ushort)(i + 1);
            pOut[0] = (byte)value;
            pOut[1] = (byte)(value >> 8);
            pOut[2] = (byte)(value >> 16);
            pOut += 3;
        }

        bool result = TryEncodeRuns(pixelCount, pInColorResponse32bpp, outEncodedCapacity,
            pOutEncoded, pOut, pValueIndices, out encodedLength);

        ClearPaletteState(pValueCounts, pPaletteValues, pValueIndices, paletteCount);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CountPaletteValues(
        int pixelCount,
        byte* pInColorResponse32bpp,
        int* pValueCounts,
        int* pPaletteValues)
    {
        int paletteCount = 0;
        byte* pSrc = pInColorResponse32bpp;
        byte* pSrcEnd = pInColorResponse32bpp + (pixelCount * 4);

        while (pSrc < pSrcEnd)
        {
            int value = GetQuantizedValue(pSrc);

            if (pValueCounts[value] == 0)
            {
                pPaletteValues[paletteCount] = value;
                paletteCount++;
            }

            pValueCounts[value]++;
            pSrc += 4;
        }

        return paletteCount;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEncodeRuns(
        int pixelCount,
        byte* pInColorResponse32bpp,
        int outEncodedCapacity,
        byte* pOutEncoded,
        byte* pOut,
        ushort* pValueIndices,
        out int encodedLength)
    {
        byte* pSrc = pInColorResponse32bpp;
        byte* pSrcEnd = pInColorResponse32bpp + (pixelCount * 4);
        int currentIndex = -1;
        int runLength = 0;

        while (pSrc < pSrcEnd)
        {
            int value = GetQuantizedValue(pSrc);
            int index = pValueIndices[value] - 1;

            if (index == currentIndex &&
                runLength < 256)
            {
                runLength++;
                pSrc += 4;
                continue;
            }

            if (runLength > 0 &&
                !TryWriteRun(currentIndex, runLength, outEncodedCapacity, pOutEncoded, ref pOut))
            {
                encodedLength = 0;
                return false;
            }

            currentIndex = index;
            runLength = 1;
            pSrc += 4;
        }

        if (runLength > 0 &&
            !TryWriteRun(currentIndex, runLength, outEncodedCapacity, pOutEncoded, ref pOut))
        {
            encodedLength = 0;
            return false;
        }

        encodedLength = (int)(pOut - pOutEncoded);
        return true;
    }

    private static bool TryDecode(
        byte* pInEncoded,
        int encodedLength,
        byte* pOutColorResponse32bpp,
        int outPixelCapacity,
        int* pPaletteValues,
        int paletteValuesCapacity,
        out PlayerColorExclusionContext playerColorExclusion)
    {
        playerColorExclusion = default;

        if (encodedLength < _HeaderLength)
        {
            return false;
        }

        uint magic = (uint)(pInEncoded[0] |
            (pInEncoded[1] << 8) |
            (pInEncoded[2] << 16) |
            (pInEncoded[3] << 24));

        if (magic != _Magic)
        {
            return false;
        }

        int pixelCount = pInEncoded[4] |
            (pInEncoded[5] << 8) |
            (pInEncoded[6] << 16) |
            (pInEncoded[7] << 24);
        byte flags = pInEncoded[8];

        if ((flags & ~(_HasTeamAFlag | _HasTeamBFlag)) != 0)
        {
            return false;
        }

        bool hasTeamA = (flags & _HasTeamAFlag) != 0;
        bool hasTeamB = (flags & _HasTeamBFlag) != 0;
        BallDetectionColorModel teamA = ReadColorModel(pInEncoded + _TeamAModelOffset);
        BallDetectionColorModel teamB = ReadColorModel(pInEncoded + _TeamBModelOffset);
        int paletteCount = pInEncoded[_PaletteCountOffset] |
            (pInEncoded[_PaletteCountOffset + 1] << 8);

        if (pixelCount > outPixelCapacity ||
            paletteCount > paletteValuesCapacity ||
            encodedLength < _HeaderLength + (paletteCount * 3))
        {
            return false;
        }

        byte* pIn = pInEncoded + _HeaderLength;
        byte* pInEnd = pInEncoded + encodedLength;

        for (int i = 0; i < paletteCount; i++)
        {
            pPaletteValues[i] = pIn[0] |
                (pIn[1] << 8) |
                (pIn[2] << 16);
            pIn += 3;
        }

        byte* pOut = pOutColorResponse32bpp;
        byte* pOutEnd = pOutColorResponse32bpp + (pixelCount * 4);

        while (pIn < pInEnd &&
            pOut < pOutEnd)
        {
            int index = ReadPaletteIndex(ref pIn, pInEnd);

            if (index < 0 ||
                index >= paletteCount ||
                pIn >= pInEnd)
            {
                return false;
            }

            int runLength = *pIn + 1;
            pIn++;

            if (pOut + (runLength * 4) > pOutEnd)
            {
                return false;
            }

            int value = pPaletteValues[index];

            for (int i = 0; i < runLength; i++)
            {
                WriteDequantizedValue(pOut, value);
                pOut += 4;
            }
        }

        if (pIn != pInEnd ||
            pOut != pOutEnd)
        {
            return false;
        }

        playerColorExclusion = new(hasTeamA, teamA, hasTeamB, teamB);
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte CreatePlayerColorExclusionFlags(PlayerColorExclusionContext playerColorExclusion)
    {
        byte flags = 0;

        if (playerColorExclusion.HasTeamA)
        {
            flags |= _HasTeamAFlag;
        }

        if (playerColorExclusion.HasTeamB)
        {
            flags |= _HasTeamBFlag;
        }

        return flags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteColorModel(byte* pOut, bool hasModel, BallDetectionColorModel model)
    {
        if (!hasModel)
        {
            WriteInt32(pOut, 0);
            WriteInt32(pOut + 4, 0);
            WriteInt32(pOut + 8, 0);
            WriteInt32(pOut + 12, 0);
            return;
        }

        WriteInt32(pOut, model.CenterCb);
        WriteInt32(pOut + 4, model.CenterCr);
        WriteInt32(pOut + 8, model.RadiusSquared);
        WriteInt32(pOut + 12, model.MinimumChromaticDistanceSquared);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static BallDetectionColorModel ReadColorModel(byte* pIn)
    {
        return new(
            ReadInt32(pIn),
            ReadInt32(pIn + 4),
            ReadInt32(pIn + 8),
            ReadInt32(pIn + 12));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInt32(byte* pOut, int value)
    {
        pOut[0] = (byte)value;
        pOut[1] = (byte)(value >> 8);
        pOut[2] = (byte)(value >> 16);
        pOut[3] = (byte)(value >> 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadInt32(byte* pIn)
    {
        return pIn[0] |
            (pIn[1] << 8) |
            (pIn[2] << 16) |
            (pIn[3] << 24);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryWriteRun(
        int index,
        int runLength,
        int outEncodedCapacity,
        byte* pOutEncoded,
        ref byte* pOut)
    {
        int requiredLength = index < 128 ? 2 : 3;

        if (pOut + requiredLength > pOutEncoded + outEncodedCapacity)
        {
            return false;
        }

        if (index < 128)
        {
            *pOut = (byte)index;
            pOut++;
        }
        else
        {
            *pOut = (byte)(0x80 | (index >> 8));
            pOut++;
            *pOut = (byte)index;
            pOut++;
        }

        *pOut = (byte)(runLength - 1);
        pOut++;

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ReadPaletteIndex(ref byte* pIn, byte* pInEnd)
    {
        if (pIn >= pInEnd)
        {
            return -1;
        }

        int firstByte = *pIn;
        pIn++;

        if ((firstByte & 0x80) == 0)
        {
            return firstByte;
        }

        if (pIn >= pInEnd)
        {
            return -1;
        }

        int secondByte = *pIn;
        pIn++;

        return ((firstByte & 0x7F) << 8) | secondByte;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SortPaletteValuesByFrequency(int* pPaletteValues, int* pValueCounts, int left, int right)
    {
        if (left >= right)
        {
            return;
        }

        int i = left;
        int j = right;
        int pivotValue = pPaletteValues[left + ((right - left) / 2)];
        int pivotCount = pValueCounts[pivotValue];

        while (i <= j)
        {
            while (ComparePaletteValues(pPaletteValues[i], pivotValue, pValueCounts, pivotCount) < 0)
            {
                i++;
            }

            while (ComparePaletteValues(pPaletteValues[j], pivotValue, pValueCounts, pivotCount) > 0)
            {
                j--;
            }

            if (i <= j)
            {
                (pPaletteValues[j], pPaletteValues[i]) = (pPaletteValues[i], pPaletteValues[j]);
                i++;
                j--;
            }
        }

        if (left < j)
        {
            SortPaletteValuesByFrequency(pPaletteValues, pValueCounts, left, j);
        }

        if (i < right)
        {
            SortPaletteValuesByFrequency(pPaletteValues, pValueCounts, i, right);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ComparePaletteValues(int value, int otherValue, int* pValueCounts, int otherCount)
    {
        int count = pValueCounts[value];

        if (count != otherCount)
        {
            return otherCount - count;
        }

        return value - otherValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ClearPaletteState(
        int* pValueCounts,
        int* pPaletteValues,
        ushort* pValueIndices,
        int paletteCount)
    {
        for (int i = 0; i < paletteCount; i++)
        {
            int value = pPaletteValues[i];
            pValueCounts[value] = 0;
            pValueIndices[value] = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetQuantizedValue(byte* pSrc)
    {
        return (pSrc[0] >> 2) << 12 |
            (pSrc[1] >> 2) << 6 |
            (pSrc[2] >> 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteDequantizedValue(byte* pOut, int value)
    {
        int r = (value >> 12) & 0x3F;
        int g = (value >> 6) & 0x3F;
        int b = value & 0x3F;

        pOut[0] = (byte)((r << 2) | (r >> 4));
        pOut[1] = (byte)((g << 2) | (g >> 4));
        pOut[2] = (byte)((b << 2) | (b >> 4));
        pOut[3] = 255;
    }
}
