// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Vision.TableScene.Processing;

public record struct VerticalChannel(double LeftX0, double LeftX1, double RightX0, double RightX1)
{
    public double LeftX0 { get; set; } = LeftX0;
    public double LeftX1 { get; set; } = LeftX1;
    public double RightX0 { get; set; } = RightX0;
    public double RightX1 { get; set; } = RightX1;
}

public static unsafe class BackgroundMasking
{
    public static void IgnoreInsideVerticalChannelMask(
      int width,
      int height,
      byte[] y8Mask,
      VerticalChannel channel)
    {
        fixed (byte* pY8Mask = y8Mask)
            IgnoreInsideVerticalChannelMask(width, height, pY8Mask, channel);
    }

    public static void IgnoreOutsideTrapeziumMask(
        int width,
        int height,
        byte[] y8Mask,
        Trapezium trapezium)
    {
        fixed (byte* pY8Mask = y8Mask)
            IgnoreOutsideTrapeziumMask(width, height, pY8Mask, trapezium);
    }

    public static void IgnoreInsideTrapeziumMask(
        int width,
        int height,
        byte[] y8Mask,
        Trapezium trapezium)
    {
        fixed (byte* pY8Mask = y8Mask)
            IgnoreInsideTrapeziumMask(width, height, pY8Mask, trapezium);
    }

    public static void IgnoreInsideRectangleRgba(
        int width,
        int height,
        byte[] rgba8888Image,
        Rectangle rectangle)
    {
        fixed (byte* pRgba8888Image = rgba8888Image)
            IgnoreInsideRectangleRgba(width, height, pRgba8888Image, rectangle);
    }

    private static void IgnoreInsideVerticalChannelMask(
      int width,
      int height,
      byte* pY8Mask,
      VerticalChannel channel)
    {
        // Assume stride == width
        int stride = width;

        int minX = (int)Math.Min(Math.Min(Math.Min(channel.LeftX0, channel.LeftX1), channel.RightX0), channel.RightX1);
        int maxX = (int)Math.Max(Math.Max(Math.Max(channel.LeftX0, channel.LeftX1), channel.RightX0), channel.RightX1);

        byte* pMask = pY8Mask;
        pMask += minX;
        int offset = stride - (maxX - minX + 1);

        for (int y = 0; y < height; y++)
        {
            int height_less_y = height - y;

            for (int x = minX; x <= maxX; x++)
            {
                bool isInside = ((channel.LeftX0 - x) * height_less_y) - ((0 - y) * (channel.LeftX1 - x)) < 0;

                if (!isInside)
                {
                    pMask++;
                    continue;
                }

                isInside = ((channel.RightX0 - x) * height_less_y) - ((0 - y) * (channel.RightX1 - x)) < 0;

                if (!isInside)
                {
                    *pMask = (byte)BackgroundPixelState.IgnoredPixel;
                }

                pMask++;
            }

            pMask += offset;
        }
    }

    private static void IgnoreOutsideTrapeziumMask(
        int width,
        int height,
        byte* pY8Mask,
        Trapezium trapezium)
    {
        // Assume stride == width

        byte* pMask = pY8Mask;

        // Setting p1 ..p4 here instead of referencing trapezium inside gives a massive performance boost (measured on Pixel 7)
        Point p1 = trapezium.UpperLeft;
        Point p2 = trapezium.UpperRight;
        Point p3 = trapezium.LowerLeft;
        Point p4 = trapezium.LowerRight;

        for (int y = 0; y < height; y++)
        {
            double p1Y_less_y = p1.Y - y;
            double p2Y_less_y = p2.Y - y;
            double p3Y_less_y = p3.Y - y;
            double p4Y_less_y = p4.Y - y;
            double height_less_y = height - y;

            for (int x = 0; x < width; x++)
            {
                bool isInside = ((0 - x) * p2Y_less_y) - (p1Y_less_y * (width - x)) > 0;

                if (!isInside)
                {
                    *pMask = (byte)BackgroundPixelState.IgnoredPixel;
                    pMask++;
                    continue;
                }

                isInside = ((0 - x) * p4Y_less_y) - (p3Y_less_y * (width - x)) < 0;

                if (!isInside)
                {
                    *pMask = (byte)BackgroundPixelState.IgnoredPixel;
                    pMask++;
                    continue;
                }

                isInside = ((p1.X - x) * height_less_y) - ((0 - y) * (p3.X - x)) < 0;

                if (!isInside)
                {
                    *pMask = (byte)BackgroundPixelState.IgnoredPixel;
                    pMask++;
                    continue;
                }

                isInside = ((p2.X - x) * height_less_y) - ((0 - y) * (p4.X - x)) > 0;

                if (!isInside)
                {
                    *pMask = (byte)BackgroundPixelState.IgnoredPixel;
                    pMask++;
                    continue;
                }

                pMask++;
            }
        }
    }

    private static void IgnoreInsideTrapeziumMask(
        int width,
        int height,
        byte* pY8Mask,
        Trapezium trapezium)
    {
        // Assume stride == width

        byte* pMask = pY8Mask;

        // Setting p1 ..p4 here instead of referencing trapezium inside gives a massive performance boost (measured on Pixel 7)
        Point p1 = trapezium.UpperLeft;
        Point p2 = trapezium.UpperRight;
        Point p3 = trapezium.LowerLeft;
        Point p4 = trapezium.LowerRight;

        for (int y = 0; y < height; y++)
        {
            double p1Y_less_y = p1.Y - y;
            double p2Y_less_y = p2.Y - y;
            double p3Y_less_y = p3.Y - y;
            double p4Y_less_y = p4.Y - y;
            double height_less_y = height - y;

            for (int x = 0; x < width; x++)
            {
                bool isInside = ((0 - x) * p2Y_less_y) - (p1Y_less_y * (width - x)) > 0;

                if (!isInside)
                {
                    pMask++;
                    continue;
                }

                isInside = ((0 - x) * p4Y_less_y) - (p3Y_less_y * (width - x)) < 0;

                if (!isInside)
                {
                    pMask++;
                    continue;
                }

                isInside = ((p1.X - x) * height_less_y) - ((0 - y) * (p3.X - x)) < 0;

                if (!isInside)
                {
                    pMask++;
                    continue;
                }

                isInside = ((p2.X - x) * height_less_y) - ((0 - y) * (p4.X - x)) > 0;

                if (isInside)
                {
                    *pMask = (byte)BackgroundPixelState.IgnoredPixel;
                }

                pMask++;
            }
        }
    }

    private static void IgnoreInsideRectangleRgba(
        int width,
        int height,
        byte* pRgba8888Image,
        Rectangle rect)
    {
        int startX = rect.X;
        int startY = rect.Y;

        // Stop coordinates: skip rows and columns. stop is exclusive
        int stopX = rect.X + rect.Width;
        int stopY = rect.Y + rect.Height;

        // Assume stride == width
        int stride = width;
        int offset = stride - (stopX - startX);

        uint* pSrc = (uint*)pRgba8888Image + (stride * startY) + startX;

        for (int row = startY; row < stopY; row++)
        {
            uint* pSrcEnd = pSrc + (stopX - startX);

            while (pSrc < pSrcEnd)
            {
                *pSrc = TableSceneModel.RgbaIgnoredPixel;
                pSrc++;
            }

            pSrc += offset;
        }
    }
}
