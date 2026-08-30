// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision;

public static unsafe class BallDetectionMaskRleCodec
{
    private const byte _ZeroRunMarker = 0x80;
    private const byte _MaxRunLength = 0x7F;

    public static int GetMaxEncodedLength(int pixelCount)
    {
        return Math.Max(0, pixelCount);
    }

    public static int Encode(
        int width,
        int height,
        byte[] inputGray8,
        byte[] outputRle)
    {
        int pixelCount = width * height;
        fixed (byte* pInputGray8 = inputGray8)
        fixed (byte* pOutputRle = outputRle)
            return Encode(pixelCount, pInputGray8, pOutputRle);
    }

    public static void DecodeToGray8(
        int width,
        int height,
        byte[] inputRle,
        int inputLength,
        byte[] outputGray8)
    {
        int pixelCount = width * height;
        fixed (byte* pInputRle = inputRle)
        fixed (byte* pOutputGray8 = outputGray8)
            DecodeToGray8(pixelCount, pInputRle, inputLength, pOutputGray8);
    }

    private static int Encode(
        int pixelCount,
        byte* pInputGray8,
        byte* pOutputRle)
    {
        byte* pSrc = pInputGray8;
        byte* pSrcEnd = pInputGray8 + pixelCount;
        byte* pOut = pOutputRle;
        byte zeroCount = 0;

        while (pSrc < pSrcEnd)
        {
            byte value = *pSrc;
            pSrc++;

            if (value == 0)
            {
                zeroCount++;

                if (zeroCount == _MaxRunLength)
                {
                    *pOut = (byte)(_ZeroRunMarker | _MaxRunLength);
                    pOut++;
                    zeroCount = 0;
                }

                continue;
            }

            if (zeroCount > 0)
            {
                *pOut = (byte)(_ZeroRunMarker | zeroCount);
                pOut++;
                zeroCount = 0;
            }

            *pOut = (byte)(value >> 1);
            pOut++;
        }

        if (zeroCount > 0)
        {
            *pOut = (byte)(_ZeroRunMarker | zeroCount);
            pOut++;
        }

        return (int)(pOut - pOutputRle);
    }

    private static void DecodeToGray8(
        int pixelCount,
        byte* pInputRle,
        int inputLength,
        byte* pOutputGray8)
    {
        byte* pIn = pInputRle;
        byte* pInEnd = pInputRle + inputLength;
        byte* pOut = pOutputGray8;
        byte* pOutEnd = pOutputGray8 + pixelCount;

        while (pIn < pInEnd &&
            pOut < pOutEnd)
        {
            byte value = *pIn;
            pIn++;

            if (value > 0x7F)
            {
                int count = value & _MaxRunLength;

                if (pOut + count > pOutEnd)
                {
                    return;
                }

                byte* pZeroEnd = pOut + count;

                while (pOut < pZeroEnd)
                {
                    *pOut = 0;
                    pOut++;
                }

                continue;
            }

            *pOut = (byte)(value << 1);
            pOut++;
        }
    }
}
