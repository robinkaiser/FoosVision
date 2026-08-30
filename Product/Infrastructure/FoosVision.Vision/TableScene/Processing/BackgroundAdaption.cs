// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;

namespace FoosVision.Vision.TableScene.Processing;

public static unsafe class BackgroundAdaption
{
    public static void ResetIgnoredPixels(
        int width,
        int height,
        byte[] inOutBackgroundState,
        byte[] inOutBackgroundMin,
        byte[] inOutBackgroundMax)
    {
        fixed (byte* pInOutBackgroundState = inOutBackgroundState)
        fixed (byte* pInOutBackgroundMin = inOutBackgroundMin)
        fixed (byte* pInOutBackgroundMax = inOutBackgroundMax)
            ResetIgnoredPixels(width, height, pInOutBackgroundState, pInOutBackgroundMin, pInOutBackgroundMax);
    }

    public static void UpdateModelFromRgba(
       int width,
       int height,
       byte[] inputRGBA8888Image,
       byte[] inOutBackgroundState,
       byte[] inOutBackgroundMin,
       byte[] inOutBackgroundMax)
    {
        fixed (byte* pInRGBA8888 = inputRGBA8888Image)
        fixed (byte* pInOutBackgroundState = inOutBackgroundState)
        fixed (byte* pInOutBackgroundMin = inOutBackgroundMin)
        fixed (byte* pInOutBackgroundMax = inOutBackgroundMax)
            UpdateModelFromRgba(width, height, pInRGBA8888, pInOutBackgroundState, pInOutBackgroundMin, pInOutBackgroundMax);
    }

    private static void ResetIgnoredPixels(
        int width,
        int height,
        byte* pInOutBackgroundState,
        byte* pInOutBackgroundMin,
        byte* pInOutBackgroundMax)
    {
        // Assume stride == width
        int imgSize = width * height;

        byte* pBgState = pInOutBackgroundState;
        byte* pBgStateEnd = pInOutBackgroundState + imgSize;
        uint* pBgMin = (uint*)pInOutBackgroundMin;
        uint* pBgMax = (uint*)pInOutBackgroundMax;

        while (pBgState < pBgStateEnd)
        {
            if (*pBgState == (byte)BackgroundPixelState.IgnoredPixel)
            {
                *pBgState = (byte)BackgroundPixelState.NotInitialized;
                *pBgMin = 0;
                *pBgMax = 0;
            }

            pBgState++;
            pBgMin++;
            pBgMax++;
        }
    }

    private static void UpdateModelFromRgba(
        int width,
        int height,
        byte* pInRGBA8888,
        byte* pInOutBackgroundState,
        byte* pInOutBackgroundMin,
        byte* pInOutBackgroundMax)
    {
        // Assume stride == width
        int imgSize = width * height;

        uint* pRgba = (uint*)pInRGBA8888;
        uint* pRgbaEnd = pRgba + imgSize;
        byte* pBgState = pInOutBackgroundState;
        uint* pBgMin = (uint*)pInOutBackgroundMin;
        uint* pBgMax = (uint*)pInOutBackgroundMax;

        uint r, g, b;
        uint rMin, rMax, gMin, gMax, bMin, bMax;
        byte state;

        while (pRgba < pRgbaEnd)
        {
            if (*pRgba == TableSceneModel.RgbaIgnoredPixel ||
                *pBgState == (byte)BackgroundPixelState.IgnoredPixel)
            {
                pRgba++;
                pBgState++;
                pBgMin++;
                pBgMax++;
                continue;
            }

            if (*pBgState == (byte)BackgroundPixelState.NotInitialized)
            {
                *pBgState = (byte)BackgroundPixelState.Ok;
                *pBgMin = *pRgba;
                *pBgMax = *pRgba;

                pRgba++;
                pBgState++;
                pBgMin++;
                pBgMax++;
                continue;
            }

            r = *pRgba & 0x000000FF;
            g = (*pRgba & 0x0000FF00) >> 8;
            b = (*pRgba & 0x00FF0000) >> 16;

            state = (byte)BackgroundPixelState.Ok;

            rMin = *pBgMin & 0x000000FF;
            gMin = (*pBgMin & 0x0000FF00) >> 8;
            bMin = (*pBgMin & 0x00FF0000) >> 16;

            rMax = *pBgMax & 0x000000FF;
            gMax = (*pBgMax & 0x0000FF00) >> 8;
            bMax = (*pBgMax & 0x00FF0000) >> 16;

            UpdateChannelColor(r, ref rMin, ref rMax, ref state);
            UpdateChannelColor(g, ref gMin, ref gMax, ref state);
            UpdateChannelColor(b, ref bMin, ref bMax, ref state);

            *pBgState = state;
            *pBgMin = 0xFF000000 | bMin << 16 | gMin << 8 | rMin;
            *pBgMax = 0xFF000000 | bMax << 16 | gMax << 8 | rMax;

            pRgba++;
            pBgState++;
            pBgMin++;
            pBgMax++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void UpdateChannelColor(uint c, ref uint min, ref uint max, ref byte state)
    {
        const double A_EXPAND = 0.4;   // Fast expand factor
        const uint D_CONTRACT = 2;     // Slow contract step (LSB)
        const uint D_EXPANDMIN = 4;    // Need to be this far outside to expand
        const uint D_CONTRACTMIN = 16; // Need to be this far inside to contract
        const uint ADAPT_THRESH = 32;  // Mark adapting on big jumps

        int dLo = (int)min - (int)c;
        int dHi = (int)c - (int)max;

        if (dLo > D_EXPANDMIN)
        {   // Expanding to lower
            if ((min - c) >= ADAPT_THRESH)
            {   // Big difference, mark pixel as expanding to lower
                state = (byte)BackgroundPixelState.AdaptingLower;
            }
            min -= (uint)(A_EXPAND * dLo);
            return;
        }

        if (dHi > D_EXPANDMIN)
        {   // Expanding to upper
            if ((c - max) >= ADAPT_THRESH)
            {   // Big difference, mark pixel as expanding to higher
                state = (byte)BackgroundPixelState.AdaptingUpper;
            }
            max += (uint)(A_EXPAND * dHi);
            return;
        }

        if (dLo < -D_CONTRACTMIN)
        {   // Slowly contracting
            min += D_CONTRACT;
        }

        if (dHi < -D_CONTRACTMIN)
        {   // Slowly contracting
            max -= D_CONTRACT;
        }
    }
}
