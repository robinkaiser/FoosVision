// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Vision.BallFinding.Processing;

public static unsafe class ImageStatistics
{
    public static int CountNonZeroGray8(
        int width,
        byte[] inGray8,
        Rectangle rect)
    {
        fixed (byte* pInGray8 = inGray8)
            return CountNonZeroGray8(width, pInGray8, rect);
    }

    private static int CountNonZeroGray8(
        int width,
        byte* pInGray8,
        Rectangle rect)
    {
        int startX = rect.X;
        int startY = rect.Y;

        // Stop coordinates: skip rows and columns. stop is exclusive
        int stopX = rect.X + rect.Width;
        int stopY = rect.Y + rect.Height;

        // TODO: Beware stride assumption
        int dstStride = width;
        int srcStride = width;

        int dstOffset = dstStride - (stopX - startX);
        int srcOffset = srcStride - (stopX - startX);

        int pixelCount = 0;

        byte* src = pInGray8 + (srcStride * startY) + startX;

        for (int row = startY; row < stopY; row++)
        {
            byte* srcEnd = src + (stopX - startX);

            while (src < srcEnd)
            {
                if (*src > 0)
                {
                    pixelCount++;
                }

                src++;
            }

            src += srcOffset;
        }

        return pixelCount;
    }
}
