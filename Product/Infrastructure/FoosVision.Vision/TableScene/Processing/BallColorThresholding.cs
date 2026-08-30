// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Vision.TableScene.Processing;

public static unsafe class BallColorThresholding
{
    private const uint _MinWhiteBallY = 128;
    private const uint _MinDarkerWhiteBallY = 192;
    private const uint _BackgroundOffsetY = 24;

    public static void InitializeColorResponse(byte[] outColorResponse32bpp, BallColor ballColor)
    {
        fixed (byte* pOutColorResponse32bpp = outColorResponse32bpp)
        {
            switch (ballColor)
            {
                case BallColor.White:
                    InitializeWhiteBallColorResponse(outColorResponse32bpp.Length / 4, pOutColorResponse32bpp);
                    break;
                case BallColor.Yellow:
                    throw new NotImplementedException();
                case BallColor.Red:
                    throw new NotImplementedException();
                case BallColor.Unknown:
                default:
                    InitializeWhiteBallColorResponse(outColorResponse32bpp.Length / 4, pOutColorResponse32bpp);
                    break;
            }
        }
    }

    public static void ComputeBallColorThresholds(
        int width,
        int height,
        byte[] inBackgroundState,
        byte[] inBackgroundMin,
        byte[] inBackgroundMax,
        byte[] outColorResponse32bpp,
        uint ignoredPixelPlaceholderValue,
        BallColor ballColor)
    {
        fixed (byte* pInBackgroundState = inBackgroundState)
        fixed (byte* pInBackgroundMin = inBackgroundMin)
        fixed (byte* pInBackgroundMax = inBackgroundMax)
        fixed (byte* pOutColorResponse32bpp = outColorResponse32bpp)
        {
            switch (ballColor)
            {
                case BallColor.White:
                    ComputeWhiteBallAcceptedYRanges(width, height, pInBackgroundState,
                        pInBackgroundMin, pInBackgroundMax, pOutColorResponse32bpp, ignoredPixelPlaceholderValue);
                    break;
                case BallColor.Yellow:
                    throw new NotImplementedException();
                case BallColor.Red:
                    throw new NotImplementedException();
                case BallColor.Unknown:
                default:
                    ComputeWhiteBallAcceptedYRanges(width, height, pInBackgroundState,
                        pInBackgroundMin, pInBackgroundMax, pOutColorResponse32bpp, ignoredPixelPlaceholderValue);
                    break;
            }
        }
    }

    private static void InitializeWhiteBallColorResponse(int pixelCount, byte* pOutColorResponse32bpp)
    {
        uint* pColorResponse = (uint*)pOutColorResponse32bpp;
        uint* pColorResponseEnd = pColorResponse + pixelCount;
        const uint AcceptedMaxY = 255;
        uint initialColorResponse = 0xFF000000 | AcceptedMaxY << 8 | _MinWhiteBallY;

        while (pColorResponse < pColorResponseEnd)
        {
            *pColorResponse = initialColorResponse;
            pColorResponse++;
        }
    }

    private static void ComputeWhiteBallAcceptedYRanges(
        int width,
        int height,
        byte* pInBackgroundState,
        byte* pInBackgroundMin,
        byte* pInBackgroundMax,
        byte* pOutColorResponse32bpp,
        uint ignoredPixelPlaceholderValue)
    {
        // Assume stride == width
        int imgSize = width * height;

        byte* pBgState = pInBackgroundState;
        byte* pBgStateEnd = pInBackgroundState + imgSize;
        uint* pBgMin = (uint*)pInBackgroundMin;
        uint* pBgMax = (uint*)pInBackgroundMax;
        uint* pColorResponse = (uint*)pOutColorResponse32bpp;

        uint r = 0, g = 0, b = 0, backgroundMinY = 0, backgroundMaxY = 0;
        uint brighterAcceptedMinY = 0, darkerAcceptedMaxY = 0, acceptedMinY = 0, acceptedMaxY = 0;

        while (pBgState < pBgStateEnd)
        {
            byte backgroundState = *pBgState;

            if (backgroundState == (byte)BackgroundPixelState.IgnoredPixel ||
                backgroundState == (byte)BackgroundPixelState.NotInitialized)
            {   //  Pixel is permanently irrelevant or not initialized yet
                *pColorResponse = ignoredPixelPlaceholderValue;
                pColorResponse++;
                pBgMin++;
                pBgMax++;
                pBgState++;
                continue;
            }

            CalculateRGBY(pBgMin, ref r, ref g, ref b, ref backgroundMinY);
            CalculateRGBY(pBgMax, ref r, ref g, ref b, ref backgroundMaxY);

            if (backgroundMinY > _BackgroundOffsetY)
            {
                darkerAcceptedMaxY = backgroundMinY - _BackgroundOffsetY;
            }
            else
            {
                darkerAcceptedMaxY = 0;
            }

            brighterAcceptedMinY = backgroundMaxY;

            if (brighterAcceptedMinY < (byte.MaxValue - _BackgroundOffsetY))
            {
                brighterAcceptedMinY += _BackgroundOffsetY;
            }
            else
            {
                brighterAcceptedMinY = 255;
            }

            if (brighterAcceptedMinY < _MinWhiteBallY)
            {
                brighterAcceptedMinY = _MinWhiteBallY;
            }

            if (darkerAcceptedMaxY >= _MinDarkerWhiteBallY)
            {    // Look for darker white-ball pixels on very bright background reflection.
                acceptedMinY = _MinDarkerWhiteBallY;
                acceptedMaxY = darkerAcceptedMaxY;

                if (backgroundState == (byte)BackgroundPixelState.AdaptingLower)
                {   // Prevent for now since background brightness is adapting to the lower
                    acceptedMinY = 255;
                    acceptedMaxY = 0;
                }
            }
            else
            {   // Look for brighter white-ball pixels when no valid darker range is available.
                acceptedMinY = brighterAcceptedMinY;
                acceptedMaxY = 255;

                if (backgroundState == (byte)BackgroundPixelState.AdaptingUpper)
                {   // Prevent for now since background brightness is adapting to the upper
                    acceptedMinY = 255;
                }
            }

            *pColorResponse = 0xFF000000 | acceptedMaxY << 8 | acceptedMinY;
            pColorResponse++;
            pBgMin++;
            pBgMax++;
            pBgState++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CalculateRGBY(uint* pArgb, ref uint r, ref uint g, ref uint b, ref uint y)
    {
        r = *pArgb & 0x000000FF;
        g = (*pArgb & 0x0000FF00) >> 8;
        b = (*pArgb & 0x00FF0000) >> 16;

        // Y = 0.299R + 0.587G + 0.114B
        y = (byte)(((76 * r) + (150 * g) + (29 * b)) / 256);
    }
}
