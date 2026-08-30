// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableConfig.Processing.HoughLines;

namespace FoosVision.Vision.TableConfig;

internal class BoundaryFinder
{
    private readonly record struct BarGap(BarType Left, BarType Right);

    private const int _MaxHBorderHeightStep = 100;
    private const double _HorizontalLineMinimumAccumulatorRatio = 0.15;
    private const int _WallBrightnessSampleStripWidth = 10;
    private const int _WallBrightnessSampleDepth = 10;

    private static readonly BarGap[] _WallBrightnessBarGaps =
    [
        new(BarType.A1, BarType.A2),
        new(BarType.A2, BarType.B3),
        new(BarType.B3, BarType.A5),
        new(BarType.B5, BarType.A3),
        new(BarType.A3, BarType.B2),
        new(BarType.B2, BarType.B1),
    ];

    private static readonly Source _Log = new("Vision.BoundaryFinder");

    private readonly int _Width;
    private readonly int _Height;
    private readonly HorizontalLineFinder _HorizontalLineFinder;
    private readonly HorizontalLineRefiner _HorizontalLineRefiner;
    private readonly HoughLine[] _HoughLines;

    public BoundaryFinder(int width, int height)
    {
        _Width = width;
        _Height = height;
        _HorizontalLineFinder = new HorizontalLineFinder(width, height, 80, 100, 1.0);
        _HorizontalLineRefiner = new HorizontalLineRefiner(width, height);
        _HoughLines = new HoughLine[LineFinder.MaxLineCount];
    }

    public (HoughLine UpperHoughLine, HoughLine LowerHoughLine)? Find(
        byte[] y8CannyBuffer,
        byte[] y8ImageBuffer,
        IReadOnlyList<Bar> bars)
    {
        int height = Convert.ToInt32(_Height * 0.25);
        int y0Upper = 0;
        int y0Lower = _Height - height;

        var upperLine = GetHorizontalLine(y8CannyBuffer, y8ImageBuffer, bars, y0Upper, height, l => (l.P0.Y + l.P1.Y) / 2, -1);
        var lowerLine = GetHorizontalLine(y8CannyBuffer, y8ImageBuffer, bars, y0Lower, height, l => -(l.P0.Y + l.P1.Y) / 2, 1);

        if (!upperLine.HasValue || !lowerLine.HasValue)
        {
            return null;
        }

        return ((HoughLine upperLine, HoughLine lowerLine)?)(upperLine, lowerLine);
    }

    private HoughLine? GetHorizontalLine(
        byte[] y8CannyBuffer,
        byte[] y8ImageBuffer,
        IReadOnlyList<Bar> bars,
        int y0,
        int height,
        Func<HoughLine, double> orderKeySelector,
        int wallSampleDirection)
    {
        // Spare out left and right area to adjust for two things:
        // - playing field is shorter than outer table border
        // - playing field corners may be lifted (Leo)
        if (!FieldDetectorMath.TryClampRectangle(_Width, _Height, _Width / 4, y0, _Width / 2, height, out Rectangle rect))
        {
            _Log.Warning("GetHorizontalLine skipped: invalid rect. Raw=({X},{Y},{Width},{Height})", _Width / 4, y0, _Width / 2, height);
            return null;
        }

        var lineCount = _HorizontalLineFinder.Find(y8CannyBuffer, rect, 0.3, 0, 0, 10, _HoughLines);
        if (lineCount >= LineFinder.MaxLineCount)
        {
            _Log.Warning("GetHorizontalLine failed: rough line result hit the buffer limit. Limit={Limit}", LineFinder.MaxLineCount);
            return null;
        }

        lineCount = FieldDetectorMath.KeepStrongAccumulatorLines(_HoughLines, lineCount, _HorizontalLineMinimumAccumulatorRatio);

        if (lineCount == 0)
        {
            _Log.Warning(
                "GetHorizontalLine failed: no rough lines remain after accumulator filtering. MinimumAccumulatorRatio={MinimumAccumulatorRatio}",
                _HorizontalLineMinimumAccumulatorRatio);
            return null;
        }

        if (lineCount == 1)
        {
            return _HorizontalLineRefiner.Refine(y8CannyBuffer, _HoughLines[0], "GetHorizontalLine");
        }

        var linesSorted = _HoughLines.Take(lineCount).OrderBy(orderKeySelector).ToArray();
        int candidateCount = 1;

        while (candidateCount < linesSorted.Length)
        {
            // Step from line to line until a gap big enough emerges.
            var current = (linesSorted[candidateCount - 1].P0.Y + linesSorted[candidateCount - 1].P1.Y) / 2;
            var next = (linesSorted[candidateCount].P0.Y + linesSorted[candidateCount].P1.Y) / 2;
            var isLineFound = Math.Abs(next - current) > _MaxHBorderHeightStep;

            if (isLineFound)
            {
                break;
            }

            candidateCount++;
        }

        return GetBrightestWallCandidate(y8CannyBuffer, y8ImageBuffer, bars, linesSorted, candidateCount, wallSampleDirection);
    }

    private HoughLine? GetBrightestWallCandidate(
        byte[] y8CannyBuffer,
        byte[] y8ImageBuffer,
        IReadOnlyList<Bar> bars,
        HoughLine[] candidates,
        int candidateCount,
        int wallSampleDirection)
    {
        HoughLine? bestCandidate = null;
        double bestScore = double.MinValue;

        for (int i = 0; i < candidateCount; i++)
        {
            var refinedCandidate = _HorizontalLineRefiner.Refine(y8CannyBuffer, candidates[i], "GetHorizontalLine");

            if (refinedCandidate is null)
            {
                continue;
            }

            double score = ScoreWallBrightness(y8ImageBuffer, bars, refinedCandidate.Value, wallSampleDirection);

            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestCandidate = refinedCandidate.Value;
        }

        return bestCandidate;
    }

    private double ScoreWallBrightness(byte[] y8ImageBuffer, IReadOnlyList<Bar> bars, HoughLine candidate, int wallSampleDirection)
    {
        double wallSum = 0.0;
        int wallCount = 0;
        double sampleAnchorY = FieldDetectorMath.GetLineMidY(candidate) + (wallSampleDirection * (_WallBrightnessSampleDepth / 2.0));

        foreach (var gap in _WallBrightnessBarGaps)
        {
            if (GetBar(bars, gap.Left) is not { } leftBar || GetBar(bars, gap.Right) is not { } rightBar)
            {
                continue;
            }

            if (!TryGetLineXAtY(leftBar.Center, sampleAnchorY, out double leftX)
                || !TryGetLineXAtY(rightBar.Center, sampleAnchorY, out double rightX))
            {
                continue;
            }

            int centerX = FieldDetectorMath.RoundToNearestInt((leftX + rightX) / 2.0);
            int x0 = centerX - (_WallBrightnessSampleStripWidth / 2);

            AddBrightnessSamples(y8ImageBuffer, candidate, x0, wallSampleDirection, ref wallSum, ref wallCount);
        }

        if (wallCount == 0)
        {
            return double.MinValue;
        }

        return wallSum / wallCount;
    }

    private void AddBrightnessSamples(
        byte[] y8ImageBuffer,
        HoughLine candidate,
        int x0,
        int sampleDirection,
        ref double sum,
        ref int count)
    {
        for (int dx = 0; dx < _WallBrightnessSampleStripWidth; dx++)
        {
            int x = x0 + dx;

            if (x < 0 || x >= _Width)
            {
                continue;
            }

            double lineY = GetLineYAtX(candidate, x);

            if (!double.IsFinite(lineY))
            {
                continue;
            }

            for (int dy = 1; dy <= _WallBrightnessSampleDepth; dy++)
            {
                int y = FieldDetectorMath.RoundToNearestInt(lineY + (sampleDirection * dy));

                if (y < 0 || y >= _Height)
                {
                    continue;
                }

                sum += y8ImageBuffer[(y * _Width) + x];
                count++;
            }
        }
    }

    private static Bar? GetBar(IReadOnlyList<Bar> bars, BarType type)
    {
        foreach (var candidate in bars)
        {
            if (candidate.Type != type)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    private static bool TryGetLineXAtY(Line line, double y, out double x)
    {
        double dy = line.P1.Y - line.P0.Y;

        if (dy == 0.0)
        {
            x = 0.0;
            return false;
        }

        x = line.P0.X + ((y - line.P0.Y) * (line.P1.X - line.P0.X) / dy);
        return double.IsFinite(x);
    }

    private static double GetLineYAtX(HoughLine line, double x)
    {
        double dx = line.P1.X - line.P0.X;

        if (dx == 0.0)
        {
            return double.NaN;
        }

        return line.P0.Y + ((x - line.P0.X) * (line.P1.Y - line.P0.Y) / dx);
    }
}
