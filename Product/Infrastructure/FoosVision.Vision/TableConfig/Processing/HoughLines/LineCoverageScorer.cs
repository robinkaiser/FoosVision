// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Vision.TableConfig.Processing.HoughLines;

public static class LineCoverageScorer
{
    public static LineCoverageScore ScoreVertical(
        byte[] inputY8EdgeImage,
        int imageWidth,
        int imageHeight,
        Rectangle rect,
        HoughLine line,
        int binCount,
        int halfWidth,
        int minimumEdgePixelsPerBin)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || binCount <= 0 || minimumEdgePixelsPerBin <= 0)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        Rectangle imageRect = new(0, 0, imageWidth, imageHeight);
        Rectangle scanRect = Rectangle.Intersect(rect, imageRect);

        if (scanRect.IsEmpty)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        int effectiveBinCount = Math.Min(binCount, scanRect.Height);
        int supportedBins = 0;
        int currentRun = 0;
        int longestRun = 0;
        int edgePixelCount = 0;

        for (int bin = 0; bin < effectiveBinCount; bin++)
        {
            int y0 = scanRect.Y + (bin * scanRect.Height / effectiveBinCount);
            int y1 = scanRect.Y + ((bin + 1) * scanRect.Height / effectiveBinCount);
            int binEdgePixelCount = CountVerticalEdgePixels(inputY8EdgeImage, imageWidth, scanRect, line, y0, y1, halfWidth);

            edgePixelCount += binEdgePixelCount;

            if (binEdgePixelCount < minimumEdgePixelsPerBin)
            {
                currentRun = 0;
                continue;
            }

            supportedBins++;
            currentRun++;
            longestRun = Math.Max(longestRun, currentRun);
        }

        return new LineCoverageScore(supportedBins, effectiveBinCount, longestRun, edgePixelCount);
    }

    public static LineCoverageScore ScoreHorizontal(
        byte[] inputY8EdgeImage,
        int imageWidth,
        int imageHeight,
        Rectangle rect,
        HoughLine line,
        int binCount,
        int halfHeight,
        int minimumEdgePixelsPerBin)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || binCount <= 0 || minimumEdgePixelsPerBin <= 0)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        Rectangle imageRect = new(0, 0, imageWidth, imageHeight);
        Rectangle scanRect = Rectangle.Intersect(rect, imageRect);

        if (scanRect.IsEmpty)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        int effectiveBinCount = Math.Min(binCount, scanRect.Width);
        int supportedBins = 0;
        int currentRun = 0;
        int longestRun = 0;
        int edgePixelCount = 0;

        for (int bin = 0; bin < effectiveBinCount; bin++)
        {
            int x0 = scanRect.X + (bin * scanRect.Width / effectiveBinCount);
            int x1 = scanRect.X + ((bin + 1) * scanRect.Width / effectiveBinCount);
            int binEdgePixelCount = CountHorizontalEdgePixels(inputY8EdgeImage, imageWidth, scanRect, line, x0, x1, halfHeight);

            edgePixelCount += binEdgePixelCount;

            if (binEdgePixelCount < minimumEdgePixelsPerBin)
            {
                currentRun = 0;
                continue;
            }

            supportedBins++;
            currentRun++;
            longestRun = Math.Max(longestRun, currentRun);
        }

        return new LineCoverageScore(supportedBins, effectiveBinCount, longestRun, edgePixelCount);
    }

    private static int CountVerticalEdgePixels(
        byte[] inputY8EdgeImage,
        int imageWidth,
        Rectangle scanRect,
        HoughLine line,
        int y0,
        int y1,
        int halfWidth)
    {
        int edgePixelCount = 0;

        for (int y = y0; y < y1; y++)
        {
            double lineX = GetLineX(line, y);

            if (!double.IsFinite(lineX))
            {
                continue;
            }

            int x0 = Math.Max(scanRect.X, (int)Math.Floor(lineX) - halfWidth);
            int x1 = Math.Min(scanRect.RightExclusive - 1, (int)Math.Ceiling(lineX) + halfWidth);

            for (int x = x0; x <= x1; x++)
            {
                if (inputY8EdgeImage[(y * imageWidth) + x] > 0)
                {
                    edgePixelCount++;
                }
            }
        }

        return edgePixelCount;
    }

    private static int CountHorizontalEdgePixels(
        byte[] inputY8EdgeImage,
        int imageWidth,
        Rectangle scanRect,
        HoughLine line,
        int x0,
        int x1,
        int halfHeight)
    {
        int edgePixelCount = 0;

        for (int x = x0; x < x1; x++)
        {
            double lineY = GetLineY(line, x);

            if (!double.IsFinite(lineY))
            {
                continue;
            }

            int y0 = Math.Max(scanRect.Y, (int)Math.Floor(lineY) - halfHeight);
            int y1 = Math.Min(scanRect.BottomExclusive - 1, (int)Math.Ceiling(lineY) + halfHeight);

            for (int y = y0; y <= y1; y++)
            {
                if (inputY8EdgeImage[(y * imageWidth) + x] <= 0)
                {
                    continue;
                }

                edgePixelCount++;
                break;
            }
        }

        return edgePixelCount;
    }

    private static double GetLineX(HoughLine line, int y)
    {
        double dy = line.P1.Y - line.P0.Y;

        if (dy == 0.0)
        {
            return double.NaN;
        }

        return line.P0.X + ((y - line.P0.Y) * (line.P1.X - line.P0.X) / dy);
    }

    private static double GetLineY(HoughLine line, int x)
    {
        double dx = line.P1.X - line.P0.X;

        if (dx == 0.0)
        {
            return double.NaN;
        }

        return line.P0.Y + ((x - line.P0.X) * (line.P1.Y - line.P0.Y) / dx);
    }
}
