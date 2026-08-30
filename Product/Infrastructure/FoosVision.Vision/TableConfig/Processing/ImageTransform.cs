// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Vision.TableConfig.Processing;

public static unsafe class ImageTransform
{
    public static void ConvertRGBA8888ToGray8(
        int width,
        byte[] inRGBA8888,
        byte[] outGray8,
        Rectangle rect)
    {
        fixed (byte* pInRGBA8888 = inRGBA8888)
        fixed (byte* pOutGray8 = outGray8)
            ConvertRGBA8888ToGray8(width, pInRGBA8888, pOutGray8, rect);
    }

    private static void ConvertRGBA8888ToGray8(
        int imageWidth,
        byte* pInRGBA8888,
        byte* pOutGray8,
        Rectangle rect)
    {
        int startX = rect.X;
        int startY = rect.Y;

        // Stop is exclusive
        int stopX = rect.X + rect.Width;
        int stopY = rect.Y + rect.Height;
        int widthX = stopX - startX;

        // TODO: Beware stride assumption
        int dstStride = imageWidth;
        int srcStride = imageWidth;

        int dstOffset = dstStride - widthX;
        int srcOffset = srcStride - widthX;

        uint* pSrc = (uint*)pInRGBA8888 + (srcStride * startY) + startX;
        byte* pDst = pOutGray8 + (dstStride * startY) + startX;

        uint r, g, b;

        for (int row = startY; row < stopY; row++)
        {
            uint* srcEnd = pSrc + widthX;

            while (pSrc < srcEnd)
            {
                r = *pSrc & 0x000000FF;
                g = (*pSrc & 0x0000FF00) >> 8;
                b = (*pSrc & 0x00FF0000) >> 16;

                // Y = 0.299R + 0.587G + 0.114B
                *pDst = (byte)(((76 * r) + (150 * g) + (29 * b)) >> 8);

                pSrc++;
                pDst++;
            }

            pSrc += srcOffset;
            pDst += dstOffset;
        }
    }
}
