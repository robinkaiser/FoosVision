// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.ColoredPlayers;
using FoosVision.Vision.ValidationTests.Utils;

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class EdgeScoreProfileWriter
{
    public static void Write(ColoredRodObjectIntervalDetection detection, string outputPath)
    {
        int width = GetMaxEdgeScoreCount(detection);
        int rowHeight = 96;
        int rowGap = 8;
        int height = (detection.Rods.Count * rowHeight) + ((detection.Rods.Count - 1) * rowGap);
        double maxScore = GetMaxEdgeScore(detection);
        byte[] image = new byte[width * height * 4];

        Fill(image, width, height, 255, 255, 255);

        for (int rodIndex = 0; rodIndex < detection.Rods.Count; rodIndex++)
        {
            RodColoredObjectIntervals rod = detection.Rods[rodIndex];
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
                GetScoreY(rod.EdgeScoreProfile.MinimumScore, maxScore, graphTop, graphBottom),
                8,
                5,
                180,
                0,
                180);

            DrawOcclusions(image, width, height, rod, graphTop, graphBottom);
            DrawIntervals(image, width, height, rod, y0, graphTop, graphBottom);
            DrawScores(image, width, height, rod, maxScore, graphTop, graphBottom);
        }

        ValidationImageUtils.WriteRGBA8888ImageToFile(image, width, height, outputPath);
    }

    private static void DrawOcclusions(
        byte[] image,
        int width,
        int height,
        RodColoredObjectIntervals rod,
        int graphTop,
        int graphBottom)
    {
        for (int i = 0; i < rod.SampleProfile.Count && i < rod.EdgeScoreProfile.Count; i++)
        {
            if (!rod.SampleProfile.Occluded[i])
            {
                continue;
            }

            int x = ScaleIndex(i, rod.EdgeScoreProfile.Count, width);
            FillRect(image, width, height, x, graphTop, 1, graphBottom - graphTop + 1, 210, 230, 255);
        }
    }

    private static void DrawIntervals(
        byte[] image,
        int width,
        int height,
        RodColoredObjectIntervals rod,
        int y0,
        int graphTop,
        int graphBottom)
    {
        foreach (var interval in rod.Intervals)
        {
            int startX = ScaleIndex(interval.StartIndex, rod.EdgeScoreProfile.Count, width);
            int endX = ScaleIndex(interval.EndIndex, rod.EdgeScoreProfile.Count, width);

            FillRect(image, width, height, startX, y0 + 1, Math.Max(1, endX - startX + 1), 6, 255, 0, 255);
            DrawVerticalLine(image, width, height, startX, graphTop, graphBottom, 255, 0, 255);
            DrawVerticalLine(image, width, height, endX, graphTop, graphBottom, 255, 0, 255);
        }
    }

    private static void DrawScores(
        byte[] image,
        int width,
        int height,
        RodColoredObjectIntervals rod,
        double maxScore,
        int graphTop,
        int graphBottom)
    {
        for (int i = 0; i < rod.EdgeScoreProfile.Count; i++)
        {
            int x = ScaleIndex(i, rod.EdgeScoreProfile.Count, width);
            int y = GetScoreY(rod.EdgeScoreProfile.Scores[i], maxScore, graphTop, graphBottom);

            DrawVerticalLine(image, width, height, x, y, graphBottom, 30, 30, 30);
        }
    }

    private static int GetMaxEdgeScoreCount(ColoredRodObjectIntervalDetection detection)
    {
        int max = 1;

        foreach (var rod in detection.Rods)
        {
            max = Math.Max(max, rod.EdgeScoreProfile.Count);
        }

        return max;
    }

    private static double GetMaxEdgeScore(ColoredRodObjectIntervalDetection detection)
    {
        double max = 1;

        foreach (var rod in detection.Rods)
        {
            max = Math.Max(max, GetMaxEdgeScore(rod));
        }

        return max;
    }

    private static double GetMaxEdgeScore(RodColoredObjectIntervals rod)
    {
        double max = 0;

        for (int i = 0; i < rod.EdgeScoreProfile.Count; i++)
        {
            max = Math.Max(max, rod.EdgeScoreProfile.Scores[i]);
        }

        return max;
    }

    private static int ScaleIndex(int index, int count, int width)
        => count <= 1 ? 0 : Convert.ToInt32((double)index / (count - 1) * (width - 1));

    private static int GetScoreY(double score, double maxScore, int graphTop, int graphBottom)
    {
        double normalized = Math.Clamp(score / maxScore, 0, 1);

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

    private static void SetPixel(byte[] image, int width, int x, int y, byte r, byte g, byte b)
    {
        int offset = ((y * width) + x) * 4;

        image[offset + 0] = r;
        image[offset + 1] = g;
        image[offset + 2] = b;
        image[offset + 3] = byte.MaxValue;
    }
}
