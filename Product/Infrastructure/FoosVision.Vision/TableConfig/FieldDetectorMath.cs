// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Vision.TableConfig.Processing.HoughLines;

namespace FoosVision.Vision.TableConfig;

internal static class FieldDetectorMath
{
    public static int KeepStrongAccumulatorLines(HoughLine[] lines, int count, double minimumAccumulatorRatio)
    {
        if (count == 0)
        {
            return 0;
        }

        int maxAccumulator = 0;

        for (int i = 0; i < count; i++)
        {
            maxAccumulator = Math.Max(maxAccumulator, lines[i].Accumulator);
        }

        double minimumAccumulator = maxAccumulator * minimumAccumulatorRatio;
        int keptCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (lines[i].Accumulator < minimumAccumulator)
            {
                continue;
            }

            lines[keptCount] = lines[i];
            keptCount++;
        }

        return keptCount;
    }

    public static bool TryClampRectangle(
        int imageWidth,
        int imageHeight,
        int x0,
        int y0,
        int width,
        int height,
        out Rectangle rectangle)
    {
        int x1 = x0 + width;
        int y1 = y0 + height;

        int clampedX0 = Math.Clamp(x0, 0, imageWidth);
        int clampedY0 = Math.Clamp(y0, 0, imageHeight);
        int clampedX1 = Math.Clamp(x1, 0, imageWidth);
        int clampedY1 = Math.Clamp(y1, 0, imageHeight);

        int clampedWidth = clampedX1 - clampedX0;
        int clampedHeight = clampedY1 - clampedY0;

        rectangle = new(clampedX0, clampedY0, clampedWidth, clampedHeight);
        return clampedWidth >= 3 && clampedHeight >= 3;
    }

    public static double GetLineMidX(Line line)
    {
        return (line.P0.X + line.P1.X) / 2.0;
    }

    public static double GetLineMidY(HoughLine line)
    {
        return (line.P0.Y + line.P1.Y) / 2.0;
    }

    public static int RoundToNearestInt(double value)
    {
        return value >= 0.0 ? (int)(value + 0.5) : (int)(value - 0.5);
    }
}
