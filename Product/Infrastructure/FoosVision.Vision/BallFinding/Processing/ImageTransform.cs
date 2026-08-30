// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.Common;

namespace FoosVision.Vision.BallFinding.Processing;

public static unsafe class ImageTransform
{
    public static void ConvertRGBA8888toGray8(
        int width,
        byte[] inRGBA8888,
        byte[] inColorResponse32bpp,
        byte[] outGray8,
        Rectangle rect,
        uint ignoredPixelPlaceholderValue,
        BallColor ballColor,
        PlayerColorExclusionContext playerColorExclusion)
    {
        fixed (byte* pInRGBA8888 = inRGBA8888)
        fixed (byte* pInColorResponse32bpp = inColorResponse32bpp)
        fixed (byte* pOutGray = outGray8)
        {
            switch (ballColor)
            {
                case BallColor.White:
                    ConvertRGBA8888toGray8WhiteBall(width, pInRGBA8888, pInColorResponse32bpp, pOutGray, rect,
                        ignoredPixelPlaceholderValue, playerColorExclusion);
                    break;
                case BallColor.Yellow:
                    throw new NotImplementedException();
                case BallColor.Red:
                    throw new NotImplementedException();
                case BallColor.Unknown:
                default:
                    ConvertRGBA8888toGray8WhiteBall(width, pInRGBA8888, pInColorResponse32bpp, pOutGray, rect,
                        ignoredPixelPlaceholderValue, playerColorExclusion);
                    break;
            }
        }
    }

    public static void ConvertYuv420ToRGBA8888(
        byte[] inY,
        byte[] inU,
        byte[] inV,
        int imageWidth,
        int yRowStride,
        int yPixelStride,
        int uRowStride,
        int uPixelStride,
        int vRowStride,
        int vPixelStride,
        byte[] outRGBA8888,
        Rectangle rect)
    {
        fixed (byte* pInY = inY)
        fixed (byte* pInU = inU)
        fixed (byte* pInV = inV)
        fixed (byte* pOutRGBA8888 = outRGBA8888)
        {
            ConvertYuv420ToRGBA8888(
                pInY,
                pInU,
                pInV,
                imageWidth,
                yRowStride,
                yPixelStride,
                uRowStride,
                uPixelStride,
                vRowStride,
                vPixelStride,
                pOutRGBA8888,
                rect);
        }
    }

    private static void ConvertRGBA8888toGray8WhiteBall(
        int imageWidth,
        byte* pInRGBA8888,
        byte* pInColorResponse32bpp,
        byte* pOutGray,
        Rectangle rect,
        uint ignoredPixelPlaceholderValue,
        PlayerColorExclusionContext playerColorExclusion)
    {
        int startX = rect.X;
        int startY = rect.Y;

        // Stop is exclusive
        int stopX = rect.X + rect.Width;
        int stopY = rect.Y + rect.Height;
        int widthX = stopX - startX;

        // TODO: Beware stride assumption
        int dstStride = imageWidth;
        int srcStride = imageWidth * 4;
        int contextStride = imageWidth;

        int dstOffset = dstStride - widthX;
        int srcOffset = srcStride - (widthX * 4);
        int contextOffset = contextStride - widthX;

        byte* pSrc = pInRGBA8888 + (srcStride * startY) + (startX * 4);
        byte* pDst = pOutGray + (dstStride * startY) + startX;
        uint* pColorResponse = (uint*)pInColorResponse32bpp + (contextStride * startY) + startX;

        uint r = 0, g = 0, b = 0, y = 0, colorResponse = 0, acceptedMinY = 0, acceptedMaxY = 0;

        for (int row = startY; row < stopY; row++)
        {
            byte* srcEnd = pSrc + (widthX * 4);

            while (pSrc < srcEnd)
            {
                colorResponse = *pColorResponse;
                pColorResponse++;

                if (colorResponse == ignoredPixelPlaceholderValue)
                {   // Source pixel needs to be skipped
                    *pDst = 0;
                    pSrc += 4;
                    pDst++;
                    continue;
                }

                r = *pSrc;
                pSrc++;

                g = *pSrc;
                pSrc++;

                b = *pSrc;
                pSrc += 2;

                // Y = 0.299R + 0.587G + 0.114B
                y = (byte)(((76 * r) + (150 * g) + (29 * b)) >> 8);

                acceptedMinY = colorResponse & 0x000000FF;

                if (y < acceptedMinY)
                {   // Below accepted white-ball brightness range
                    *pDst = 0;
                    pDst++;
                    continue;
                }

                acceptedMaxY = (colorResponse & 0x0000FF00) >> 8;

                if (y > acceptedMaxY)
                {   // Above accepted white-ball brightness range
                    *pDst = 0;
                    pDst++;
                    continue;
                }

                if (playerColorExclusion.MatchesRgb((int)r, (int)g, (int)b))
                {   // Player color
                    *pDst = 0;
                    pDst++;
                    continue;
                }

                // Potential ball pixel
                *pDst = (byte)y;
                pDst++;
            }

            pSrc += srcOffset;
            pDst += dstOffset;
            pColorResponse += contextOffset;
        }
    }

    private static void ConvertYuv420ToRGBA8888(
        byte* pInY,
        byte* pInU,
        byte* pInV,
        int imageWidth,
        int yRowStride,
        int yPixelStride,
        int uRowStride,
        int uPixelStride,
        int vRowStride,
        int vPixelStride,
        byte* pOutRGBA8888,
        Rectangle rect)
    {
        int startX = rect.X;
        int startY = rect.Y;

        // Stop is exclusive
        int stopX = rect.X + rect.Width;
        int stopY = rect.Y + rect.Height;
        int widthX = stopX - startX;

        int dstStride = imageWidth * 4;
        int dstOffset = dstStride - (widthX * 4);

        byte* pDst = pOutRGBA8888 + (dstStride * startY) + (startX * 4);

        for (int y = startY; y < stopY; y++)
        {
            byte* pY = pInY + (yRowStride * y) + (startX * yPixelStride);
            byte* pU = pInU + (uRowStride * (y / 2)) + ((startX / 2) * uPixelStride);
            byte* pV = pInV + (vRowStride * (y / 2)) + ((startX / 2) * vPixelStride);

            for (int x = startX; x < stopX; x++)
            {
                int yValue = *pY;
                int uValue = *pU;
                int vValue = *pV;

                int c = yValue - 16;
                if (c < 0) c = 0;

                int d = uValue - 128;
                int e = vValue - 128;

                *pDst = ClampToByte(((298 * c) + (409 * e) + 128) >> 8);
                pDst++;

                *pDst = ClampToByte(((298 * c) - (100 * d) - (208 * e) + 128) >> 8);
                pDst++;

                *pDst = ClampToByte(((298 * c) + (516 * d) + 128) >> 8);
                pDst++;

                *pDst = 255;
                pDst++;

                pY += yPixelStride;
                if ((x & 1) != 0)
                {
                    pU += uPixelStride;
                    pV += vPixelStride;
                }
            }

            pDst += dstOffset;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampToByte(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        if (value > byte.MaxValue)
        {
            return byte.MaxValue;
        }

        return (byte)value;
    }
}
