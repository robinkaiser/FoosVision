// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.ValidationTests.Utils;

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class BlackSideBandProfileWriter
{
    public static void Write(BlackRodObjectIntervalDetection detection, string outputPath)
    {
        int width = GetMaxSampleCount(detection);
        int rowHeight = 96;
        int rowGap = 8;
        int height = (detection.Rods.Count * rowHeight) + ((detection.Rods.Count - 1) * rowGap);
        byte[] image = new byte[width * height * 4];

        Fill(image, width, height, 255, 255, 255);

        for (int rodIndex = 0; rodIndex < detection.Rods.Count; rodIndex++)
        {
            RodBlackObjectIntervals rod = detection.Rods[rodIndex];
            int y0 = rodIndex * (rowHeight + rowGap);
            int graphTop = y0 + 8;
            int graphBottom = y0 + rowHeight - 10;

            FillRect(image, width, height, 0, y0, width, rowHeight, 245, 245, 245);
            DrawHorizontalLine(image, width, height, y0, 80, 80, 80);
            DrawHorizontalLine(image, width, height, graphBottom, 200, 200, 200);
            DrawDashedHorizontalLine(
                image,
                width,
                height,
                GetY(detection.Rule.MaximumObjectY, graphTop, graphBottom),
                8,
                5,
                180,
                0,
                180);

            DrawIgnored(image, width, height, rod, graphTop, graphBottom);
            DrawIntervals(image, width, height, rod, y0, graphTop, graphBottom);
            DrawSamples(image, width, height, rod, graphTop, graphBottom);
        }

        ValidationImageUtils.WriteRGBA8888ImageToFile(image, width, height, outputPath);
    }

    private static void DrawIgnored(
        byte[] image,
        int width,
        int height,
        RodBlackObjectIntervals rod,
        int graphTop,
        int graphBottom)
    {
        for (int i = 0; i < rod.SampleProfile.Count; i++)
        {
            if (!rod.SampleProfile.Ignored[i])
            {
                continue;
            }

            int x = ScaleIndex(i, rod.SampleProfile.Count, width);
            FillRect(image, width, height, x, graphTop, 1, graphBottom - graphTop + 1, 210, 230, 255);
        }
    }

    private static void DrawIntervals(
        byte[] image,
        int width,
        int height,
        RodBlackObjectIntervals rod,
        int y0,
        int graphTop,
        int graphBottom)
    {
        foreach (var interval in rod.Intervals)
        {
            int startX = ScaleIndex(interval.StartIndex, rod.SampleProfile.Count, width);
            int endX = ScaleIndex(interval.EndIndex, rod.SampleProfile.Count, width);

            FillRect(image, width, height, startX, y0 + 1, Math.Max(1, endX - startX + 1), 6, 255, 0, 255);
            DrawVerticalLine(image, width, height, startX, graphTop, graphBottom, 255, 0, 255);
            DrawVerticalLine(image, width, height, endX, graphTop, graphBottom, 255, 0, 255);
        }
    }

    private static void DrawSamples(
        byte[] image,
        int width,
        int height,
        RodBlackObjectIntervals rod,
        int graphTop,
        int graphBottom)
    {
        DrawSampleLine(
            image,
            width,
            height,
            rod.SampleProfile.LeftY,
            rod.SampleProfile.LeftValid,
            rod.SampleProfile.Count,
            graphTop,
            graphBottom,
            0,
            80,
            220);
        DrawSampleLine(
            image,
            width,
            height,
            rod.SampleProfile.RightY,
            rod.SampleProfile.RightValid,
            rod.SampleProfile.Count,
            graphTop,
            graphBottom,
            0,
            150,
            60);

        for (int i = 0; i < rod.SampleProfile.Count; i++)
        {
            int x = ScaleIndex(i, rod.SampleProfile.Count, width);

            if (!rod.SampleProfile.Matches[i])
            {
                continue;
            }

            DrawVerticalLine(image, width, height, x, graphTop, graphTop + 5, 255, 0, 255);
        }
    }

    private static void DrawSampleLine(
        byte[] image,
        int width,
        int height,
        int[] values,
        bool[] valid,
        int count,
        int graphTop,
        int graphBottom,
        byte r,
        byte g,
        byte b)
    {
        bool hasPrevious = false;
        int previousX = 0;
        int previousY = 0;

        for (int i = 0; i < count; i++)
        {
            if (!valid[i])
            {
                hasPrevious = false;
                continue;
            }

            int x = ScaleIndex(i, count, width);
            int y = GetY(values[i], graphTop, graphBottom);

            if (hasPrevious)
            {
                DrawLine(image, width, height, previousX, previousY, x, y, r, g, b);
            }
            else
            {
                SetPixelIfInside(image, width, height, x, y, r, g, b);
            }

            previousX = x;
            previousY = y;
            hasPrevious = true;
        }
    }

    private static int GetMaxSampleCount(BlackRodObjectIntervalDetection detection)
    {
        int max = 1;

        foreach (var rod in detection.Rods)
        {
            max = Math.Max(max, rod.SampleProfile.Count);
        }

        return max;
    }

    private static int ScaleIndex(int index, int count, int width)
        => count <= 1 ? 0 : Convert.ToInt32((double)index / (count - 1) * (width - 1));

    private static int GetY(int value, int graphTop, int graphBottom)
    {
        double normalized = Math.Clamp(value / 255.0, 0, 1);

        return graphBottom - Convert.ToInt32(normalized * (graphBottom - graphTop));
    }

    private static void Fill(byte[] image, int width, int height, byte r, byte g, byte b)
        => FillRect(image, width, height, 0, 0, width, height, r, g, b);

    private static void FillRect(
        byte[] image,
        int width,
        int height,
        int x,
        int y,
        int rectWidth,
        int rectHeight,
        byte r,
        byte g,
        byte b)
    {
        int x0 = Math.Clamp(x, 0, width);
        int y0 = Math.Clamp(y, 0, height);
        int x1 = Math.Clamp(x + rectWidth, 0, width);
        int y1 = Math.Clamp(y + rectHeight, 0, height);

        for (int yy = y0; yy < y1; yy++)
        {
            for (int xx = x0; xx < x1; xx++)
            {
                SetPixel(image, width, xx, yy, r, g, b);
            }
        }
    }

    private static void DrawHorizontalLine(byte[] image, int width, int height, int y, byte r, byte g, byte b)
    {
        if (y < 0 ||
            y >= height)
        {
            return;
        }

        for (int x = 0; x < width; x++)
        {
            SetPixel(image, width, x, y, r, g, b);
        }
    }

    private static void DrawDashedHorizontalLine(
        byte[] image,
        int width,
        int height,
        int y,
        int dashLength,
        int gapLength,
        byte r,
        byte g,
        byte b)
    {
        if (y < 0 ||
            y >= height)
        {
            return;
        }

        int period = dashLength + gapLength;

        for (int x = 0; x < width; x++)
        {
            if (x % period >= dashLength)
            {
                continue;
            }

            SetPixel(image, width, x, y, r, g, b);
        }
    }

    private static void DrawVerticalLine(
        byte[] image,
        int width,
        int height,
        int x,
        int y0,
        int y1,
        byte r,
        byte g,
        byte b)
    {
        if (x < 0 ||
            x >= width)
        {
            return;
        }

        int startY = Math.Clamp(Math.Min(y0, y1), 0, height - 1);
        int stopY = Math.Clamp(Math.Max(y0, y1), 0, height - 1);

        for (int y = startY; y <= stopY; y++)
        {
            SetPixel(image, width, x, y, r, g, b);
        }
    }

    private static void DrawLine(
        byte[] image,
        int width,
        int height,
        int x0,
        int y0,
        int x1,
        int y1,
        byte r,
        byte g,
        byte b)
    {
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;

        while (true)
        {
            SetPixelIfInside(image, width, height, x0, y0, r, g, b);

            if (x0 == x1 &&
                y0 == y1)
            {
                return;
            }

            int error2 = 2 * error;

            if (error2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (error2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void SetPixelIfInside(byte[] image, int width, int height, int x, int y, byte r, byte g, byte b)
    {
        if (x < 0 ||
            x >= width ||
            y < 0 ||
            y >= height)
        {
            return;
        }

        SetPixel(image, width, x, y, r, g, b);
    }

    private static void SetPixel(byte[] image, int width, int x, int y, byte r, byte g, byte b)
    {
        int offset = ((y * width) + x) * 4;

        image[offset + 0] = r;
        image[offset + 1] = g;
        image[offset + 2] = b;
        image[offset + 3] = byte.MaxValue;
    }
}
