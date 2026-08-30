// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.ValidationTests.Utils;

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class BlackSideBandHistogramWriter
{
    public static void Write(BlackRodObjectIntervalDetection detection, string outputPath)
    {
        const int width = 256;
        const int height = 160;
        const int graphTop = 8;
        const int graphBottom = height - 12;
        int[] histogram = BuildHistogram(detection);
        int max = GetMax(histogram);
        byte[] image = new byte[width * height * 4];

        Fill(image, width, height, 255, 255, 255);
        DrawHorizontalLine(image, width, height, graphBottom, 200, 200, 200);

        for (int x = 0; x < histogram.Length; x++)
        {
            int barHeight = max == 0
                ? 0
                : Convert.ToInt32((double)histogram[x] / max * (graphBottom - graphTop));

            DrawVerticalLine(image, width, height, x, graphBottom - barHeight, graphBottom, 40, 40, 40);
        }

        DrawVerticalLine(image, width, height, detection.Rule.SearchMinimumY, graphTop, graphBottom, 0, 150, 60);
        DrawVerticalLine(image, width, height, detection.Rule.SearchMaximumY, graphTop, graphBottom, 0, 150, 60);
        DrawVerticalLine(image, width, height, detection.Rule.PercentileObjectY, graphTop, graphBottom, 80, 80, 80);
        DrawVerticalLine(image, width, height, detection.Rule.MaximumObjectY, graphTop, graphBottom, 180, 0, 180);

        ValidationImageUtils.WriteRGBA8888ImageToFile(image, width, height, outputPath);
    }

    private static int[] BuildHistogram(BlackRodObjectIntervalDetection detection)
    {
        int[] histogram = new int[256];

        foreach (var rod in detection.Rods)
        {
            BlackSideBandSampleProfile profile = rod.SampleProfile;

            for (int i = 0; i < profile.Count; i++)
            {
                if (profile.LeftValid[i])
                {
                    histogram[profile.LeftY[i]]++;
                }

                if (profile.RightValid[i])
                {
                    histogram[profile.RightY[i]]++;
                }
            }
        }

        return histogram;
    }

    private static int GetMax(int[] histogram)
    {
        int max = 1;

        for (int i = 0; i < histogram.Length; i++)
        {
            max = Math.Max(max, histogram[i]);
        }

        return max;
    }

    private static void Fill(byte[] image, int width, int height, byte r, byte g, byte b)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                SetPixel(image, width, x, y, r, g, b);
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
