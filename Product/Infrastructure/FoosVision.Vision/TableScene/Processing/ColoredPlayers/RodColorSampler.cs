// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public readonly record struct RodColorSampleProfile(BPoint[] Centers, ColorFeature[] Features, int Count);

public unsafe class RodColorSampler
{
    private readonly int _Width;
    private readonly int _Height;
    private readonly BPoint[] _CenterPoints;
    private readonly BPoint[] _CrossSectionPoints;
    private readonly BPoint[] _Centers;
    private readonly ColorFeature[] _Features;
    private readonly int[] _CbScratch;
    private readonly int[] _CrScratch;

    public RodColorSampler(int width, int height)
    {
        _Width = width;
        _Height = height;

        int lineCapacity = Math.Max(width, height);
        _CenterPoints = new BPoint[lineCapacity];
        _CrossSectionPoints = new BPoint[width + height];
        _Centers = new BPoint[lineCapacity];
        _Features = new ColorFeature[lineCapacity];
        _CbScratch = new int[width + height];
        _CrScratch = new int[width + height];
    }

    public RodColorSampleProfile Sample(byte[] frameBufferRGBA8888, Bar bar)
    {
        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (BPoint* pCenterPoints = _CenterPoints)
        fixed (BPoint* pCrossSectionPoints = _CrossSectionPoints)
            return Sample(pFrame, pCenterPoints, pCrossSectionPoints, bar);
    }

    private RodColorSampleProfile Sample(
        byte* pFrame,
        BPoint* pCenterPoints,
        BPoint* pCrossSectionPoints,
        Bar bar)
    {
        int x0 = Convert.ToInt32(bar.Center.P0.X);
        int y0 = Convert.ToInt32(bar.Center.P0.Y);
        int x1 = Convert.ToInt32(bar.Center.P1.X);
        int y1 = Convert.ToInt32(bar.Center.P1.Y);
        BLine centerLine = new(new(x0, y0), new(x1, y1));

        int centerPointCount = Bresenham.GetPoints(centerLine, pCenterPoints);
        int count = 0;

        for (int i = 0; i < centerPointCount; i++)
        {
            double t = centerPointCount == 1 ? 0 : (double)i / (centerPointCount - 1);
            Point left = Interpolate(bar.Left, t);
            Point right = Interpolate(bar.Right, t);

            if (!TrySampleCrossSection(pFrame, pCrossSectionPoints, left, right, out ColorFeature feature))
            {
                continue;
            }

            _Centers[count] = pCenterPoints[i];
            _Features[count] = feature;
            count++;
        }

        return new(_Centers, _Features, count);
    }

    private bool TrySampleCrossSection(
        byte* pFrame,
        BPoint* pCrossSectionPoints,
        Point left,
        Point right,
        out ColorFeature feature)
    {
        int x0 = Convert.ToInt32(left.X);
        int y0 = Convert.ToInt32(left.Y);
        int x1 = Convert.ToInt32(right.X);
        int y1 = Convert.ToInt32(right.Y);

        int pointCount = Bresenham.GetPoints(new(new(x0, y0), new(x1, y1)), pCrossSectionPoints);
        int sampleCount = 0;

        for (int i = 0; i < pointCount; i++)
        {
            int x = pCrossSectionPoints[i].X;
            int y = pCrossSectionPoints[i].Y;

            if (x < 0 ||
                x >= _Width ||
                y < 0 ||
                y >= _Height)
            {
                continue;
            }

            byte* pPixel = pFrame + (((y * _Width) + x) * 4);
            ColorFeature sample = ColorFeature.FromRgb(pPixel[0], pPixel[1], pPixel[2]);

            _CbScratch[sampleCount] = sample.Cb;
            _CrScratch[sampleCount] = sample.Cr;
            sampleCount++;
        }

        if (sampleCount == 0)
        {
            feature = default;
            return false;
        }

        feature = new(
            Median(_CbScratch, sampleCount),
            Median(_CrScratch, sampleCount));

        return true;
    }

    private static Point Interpolate(Line line, double t)
        => new(
            line.P0.X + (line.Dx * t),
            line.P0.Y + (line.Dy * t));

    private static int Median(int[] values, int count)
    {
        Array.Sort(values, 0, count);

        return values[count / 2];
    }
}
