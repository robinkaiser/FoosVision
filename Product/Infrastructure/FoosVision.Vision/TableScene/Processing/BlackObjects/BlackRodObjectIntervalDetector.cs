// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene.Processing.BlackObjects;

public unsafe class BlackRodObjectIntervalDetector
{
    private readonly int _Width;
    private readonly int _Height;
    private readonly BlackObjectIntervalDetectionOptions _Options;
    private readonly BPoint[] _CenterPoints;
    private readonly BPoint[] _BandPoints;
    private readonly int[] _YScratch;
    private readonly RodObjectInterval[] _IntervalBuffer;
    private readonly RodBlackObjectIntervals[] _Rods;
    private readonly int[][] _SampleX;
    private readonly int[][] _SampleY;
    private readonly bool[][] _Ignored;
    private readonly int[][] _LeftY;
    private readonly int[][] _RightY;
    private readonly bool[][] _LeftValid;
    private readonly bool[][] _RightValid;
    private readonly bool[][] _Matches;
    private readonly RodObjectInterval[][] _Intervals;
    private readonly ReadOnlyBuffer<RodObjectInterval>[] _IntervalLists;
    private readonly int[] _RuleValues;
    private readonly int[] _RuleHistogram;

    public BlackRodObjectIntervalDetector(int width, int height, BlackObjectIntervalDetectionOptions? options = null)
    {
        _Width = width;
        _Height = height;
        _Options = options ?? new BlackObjectIntervalDetectionOptions();

        int lineCapacity = Math.Max(width, height);
        _CenterPoints = new BPoint[lineCapacity];
        _BandPoints = new BPoint[width + height];
        _YScratch = new int[width + height];
        _IntervalBuffer = new RodObjectInterval[lineCapacity];
        _Rods = new RodBlackObjectIntervals[8];
        _SampleX = new int[8][];
        _SampleY = new int[8][];
        _Ignored = new bool[8][];
        _LeftY = new int[8][];
        _RightY = new int[8][];
        _LeftValid = new bool[8][];
        _RightValid = new bool[8][];
        _Matches = new bool[8][];
        _Intervals = new RodObjectInterval[8][];
        _IntervalLists = new ReadOnlyBuffer<RodObjectInterval>[8];
        _RuleValues = new int[lineCapacity * 16];
        _RuleHistogram = new int[256];

        for (int i = 0; i < 8; i++)
        {
            _SampleX[i] = new int[lineCapacity];
            _SampleY[i] = new int[lineCapacity];
            _Ignored[i] = new bool[lineCapacity];
            _LeftY[i] = new int[lineCapacity];
            _RightY[i] = new int[lineCapacity];
            _LeftValid[i] = new bool[lineCapacity];
            _RightValid[i] = new bool[lineCapacity];
            _Matches[i] = new bool[lineCapacity];
            _Intervals[i] = new RodObjectInterval[lineCapacity];
            _IntervalLists[i] = new(_Intervals[i]);
        }
    }

    public BlackRodObjectIntervalDetection Detect(
        byte[] frameBufferRGBA8888,
        PlayingField field,
        IReadOnlyList<RodObjectMask> ignoredMasks,
        bool hasTwoColoredTeamModels)
    {
        int count = 0;

        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (BPoint* pCenterPoints = _CenterPoints)
        fixed (BPoint* pBandPoints = _BandPoints)
        {
            Rectangle fieldBounds = CreateBounds(field.Boundary, _Width, _Height);

            _Rods[count] = SampleBar(pFrame, pCenterPoints, pBandPoints, field.Bars.A1, fieldBounds, field.Occlusions, ignoredMasks, count);
            count++;
            _Rods[count] = SampleBar(pFrame, pCenterPoints, pBandPoints, field.Bars.A2, fieldBounds, field.Occlusions, ignoredMasks, count);
            count++;
            _Rods[count] = SampleBar(pFrame, pCenterPoints, pBandPoints, field.Bars.B3, fieldBounds, field.Occlusions, ignoredMasks, count);
            count++;
            _Rods[count] = SampleBar(pFrame, pCenterPoints, pBandPoints, field.Bars.A5, fieldBounds, field.Occlusions, ignoredMasks, count);
            count++;
            _Rods[count] = SampleBar(pFrame, pCenterPoints, pBandPoints, field.Bars.B5, fieldBounds, field.Occlusions, ignoredMasks, count);
            count++;
            _Rods[count] = SampleBar(pFrame, pCenterPoints, pBandPoints, field.Bars.A3, fieldBounds, field.Occlusions, ignoredMasks, count);
            count++;
            _Rods[count] = SampleBar(pFrame, pCenterPoints, pBandPoints, field.Bars.B2, fieldBounds, field.Occlusions, ignoredMasks, count);
            count++;
            _Rods[count] = SampleBar(pFrame, pCenterPoints, pBandPoints, field.Bars.B1, fieldBounds, field.Occlusions, ignoredMasks, count);
            count++;
        }

        BlackObjectRule rule = CalibrateRule(_Rods, count, hasTwoColoredTeamModels);

        for (int i = 0; i < count; i++)
        {
            _Rods[i] = ApplyRule(_Rods[i], rule, i);
        }

        return new(_Rods, rule);
    }

    private RodBlackObjectIntervals SampleBar(
        byte* pFrame,
        BPoint* pCenterPoints,
        BPoint* pBandPoints,
        Bar bar,
        Rectangle fieldBounds,
        IReadOnlyList<Trapezium> occlusions,
        IReadOnlyList<RodObjectMask> ignoredMasks,
        int bufferIndex)
    {
        int x0 = RoundToInt(bar.Center.P0.X);
        int y0 = RoundToInt(bar.Center.P0.Y);
        int x1 = RoundToInt(bar.Center.P1.X);
        int y1 = RoundToInt(bar.Center.P1.Y);
        int centerPointCount = Bresenham.GetPoints(new(new(x0, y0), new(x1, y1)), pCenterPoints);

        int[] sampleX = _SampleX[bufferIndex];
        int[] sampleY = _SampleY[bufferIndex];
        bool[] ignored = _Ignored[bufferIndex];
        int[] leftY = _LeftY[bufferIndex];
        int[] rightY = _RightY[bufferIndex];
        bool[] leftValid = _LeftValid[bufferIndex];
        bool[] rightValid = _RightValid[bufferIndex];
        bool[] matches = _Matches[bufferIndex];
        int count = 0;

        for (int i = 0; i < centerPointCount; i++)
        {
            double t = centerPointCount == 1 ? 0 : (double)i / (centerPointCount - 1);
            Point left = Interpolate(bar.Left, t);
            Point right = Interpolate(bar.Right, t);

            sampleX[count] = pCenterPoints[i].X;
            sampleY[count] = pCenterPoints[i].Y;
            leftValid[count] = ShouldSampleLeftSide(bar.Type) &&
                TrySampleBand(
                    pFrame,
                    pBandPoints,
                    left,
                    right,
                    1,
                    fieldBounds,
                    occlusions,
                    ignoredMasks,
                    out leftY[count]);
            rightValid[count] = ShouldSampleRightSide(bar.Type) &&
                TrySampleBand(
                    pFrame,
                    pBandPoints,
                    right,
                    left,
                    1,
                    fieldBounds,
                    occlusions,
                    ignoredMasks,
                    out rightY[count]);
            ignored[count] = !leftValid[count] && !rightValid[count];
            count++;
        }

        return new(
            bar.Type,
            _IntervalLists[bufferIndex],
            new(sampleX, sampleY, ignored, leftY, rightY, leftValid, rightValid, matches, count));
    }

    private bool TrySampleBand(
        byte* pFrame,
        BPoint* pBandPoints,
        Point boundary,
        Point oppositeBoundary,
        int direction,
        Rectangle fieldBounds,
        IReadOnlyList<Trapezium> occlusions,
        IReadOnlyList<RodObjectMask> ignoredMasks,
        out int y)
    {
        double dx = boundary.X - oppositeBoundary.X;
        double dy = boundary.Y - oppositeBoundary.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length < 0.001)
        {
            y = 0;
            return false;
        }

        double ux = dx / length * direction;
        double uy = dy / length * direction;
        int offset = Math.Max(0, _Options.SideBandOffset);
        int width = Math.Max(1, _Options.SideBandWidth);
        Point p0 = new(
            boundary.X + (ux * offset),
            boundary.Y + (uy * offset));
        Point p1 = new(
            boundary.X + (ux * (offset + width - 1)),
            boundary.Y + (uy * (offset + width - 1)));
        int pointCount = Bresenham.GetPoints(
            new(
                new(RoundToInt(p0.X), RoundToInt(p0.Y)),
                new(RoundToInt(p1.X), RoundToInt(p1.Y))),
            pBandPoints);
        int sampleCount = 0;

        for (int i = 0; i < pointCount; i++)
        {
            int x = pBandPoints[i].X;
            int yy = pBandPoints[i].Y;

            if (x < fieldBounds.X ||
                x >= fieldBounds.RightExclusive ||
                yy < fieldBounds.Y ||
                yy >= fieldBounds.BottomExclusive ||
                IsInsideOcclusion(x, yy, occlusions) ||
                IsInsideIgnoredMask(x, yy, ignoredMasks))
            {
                continue;
            }

            byte* pPixel = pFrame + (((yy * _Width) + x) * 4);
            _YScratch[sampleCount] = GetY(pPixel[0], pPixel[1], pPixel[2]);
            sampleCount++;
        }

        if (sampleCount == 0)
        {
            y = 0;
            return false;
        }

        y = Median(_YScratch, sampleCount);
        return true;
    }

    private BlackObjectRule CalibrateRule(
        RodBlackObjectIntervals[] rods,
        int count,
        bool hasTwoColoredTeamModels)
    {
        Array.Clear(_RuleHistogram);
        int valueCount = 0;

        for (int i = 0; i < count; i++)
        {
            BlackSideBandSampleProfile profile = rods[i].SampleProfile;

            for (int j = 0; j < profile.Count; j++)
            {
                if (profile.LeftValid[j])
                {
                    _RuleValues[valueCount] = profile.LeftY[j];
                    _RuleHistogram[profile.LeftY[j]]++;
                    valueCount++;
                }

                if (profile.RightValid[j])
                {
                    _RuleValues[valueCount] = profile.RightY[j];
                    _RuleHistogram[profile.RightY[j]]++;
                    valueCount++;
                }
            }
        }

        if (valueCount == 0)
        {
            return new(
                -1,
                GetObjectPercentile(hasTwoColoredTeamModels),
                -1,
                GetSearchMinimumPercentile(hasTwoColoredTeamModels),
                GetSearchMaximumPercentile(hasTwoColoredTeamModels),
                -1,
                -1,
                Math.Max(0, _Options.SideBandOffset),
                Math.Max(1, _Options.SideBandWidth),
                Math.Max(1, _Options.MinimumRunLength),
                Math.Max(0, _Options.MaximumGapLength));
        }

        Array.Sort(_RuleValues, 0, valueCount);
        double objectPercentile = GetObjectPercentile(hasTwoColoredTeamModels);
        double searchMinimumPercentile = GetSearchMinimumPercentile(hasTwoColoredTeamModels);
        double searchMaximumPercentile = GetSearchMaximumPercentile(hasTwoColoredTeamModels);
        int percentileObjectY = _RuleValues[GetPercentileIndex(valueCount, objectPercentile)];
        int searchMinimumY = _RuleValues[GetPercentileIndex(valueCount, searchMinimumPercentile)];
        int searchMaximumY = _RuleValues[GetPercentileIndex(valueCount, searchMaximumPercentile)];
        int maximumObjectY = CalculateLocalOtsuThreshold(
            _RuleHistogram,
            searchMinimumY,
            searchMaximumY,
            percentileObjectY);

        return new(
            maximumObjectY,
            objectPercentile,
            percentileObjectY,
            searchMinimumPercentile,
            searchMaximumPercentile,
            searchMinimumY,
            searchMaximumY,
            Math.Max(0, _Options.SideBandOffset),
            Math.Max(1, _Options.SideBandWidth),
            Math.Max(1, _Options.MinimumRunLength),
            Math.Max(0, _Options.MaximumGapLength));
    }

    private RodBlackObjectIntervals ApplyRule(RodBlackObjectIntervals rod, BlackObjectRule rule, int bufferIndex)
    {
        BlackSideBandSampleProfile profile = rod.SampleProfile;

        for (int i = 0; i < profile.Count; i++)
        {
            profile.Matches[i] =
                MatchesRule(profile.LeftY[i], profile.LeftValid[i], rule) ||
                MatchesRule(profile.RightY[i], profile.RightValid[i], rule);
        }

        int intervalCount = FindIntervals(profile, rule, bufferIndex);
        _IntervalLists[bufferIndex].SetCount(intervalCount);

        return new(
            rod.BarType,
            _IntervalLists[bufferIndex],
            profile);
    }

    private int FindIntervals(BlackSideBandSampleProfile profile, BlackObjectRule rule, int bufferIndex)
    {
        int intervalCount = 0;
        bool isActive = false;
        int startIndex = 0;
        int gapCount = 0;
        double bestScore = 0;

        for (int i = 0; i < profile.Count; i++)
        {
            if (profile.Matches[i])
            {
                if (!isActive)
                {
                    isActive = true;
                    startIndex = i;
                    bestScore = 0;
                }

                gapCount = 0;
                bestScore = Math.Max(bestScore, GetObjectScore(profile, i, rule));
                continue;
            }

            if (!isActive)
            {
                continue;
            }

            gapCount++;

            if (gapCount <= rule.MaximumGapLength)
            {
                continue;
            }

            intervalCount = TryAddInterval(
                startIndex,
                i - gapCount,
                bestScore,
                rule,
                intervalCount);
            isActive = false;
            gapCount = 0;
        }

        if (isActive)
        {
            intervalCount = TryAddInterval(
                startIndex,
                profile.Count - 1 - gapCount,
                bestScore,
                rule,
                intervalCount);
        }

        for (int i = 0; i < intervalCount; i++)
        {
            _Intervals[bufferIndex][i] = _IntervalBuffer[i];
        }

        return intervalCount;
    }

    private int TryAddInterval(
        int startIndex,
        int endIndex,
        double score,
        BlackObjectRule rule,
        int intervalCount)
    {
        if (endIndex < startIndex ||
            endIndex - startIndex + 1 < rule.MinimumRunLength ||
            intervalCount >= _IntervalBuffer.Length)
        {
            return intervalCount;
        }

        _IntervalBuffer[intervalCount] = new(startIndex, endIndex, score);

        return intervalCount + 1;
    }

    private static bool MatchesRule(int y, bool isValid, BlackObjectRule rule)
        => isValid &&
            y <= rule.MaximumObjectY;

    private static bool ShouldSampleLeftSide(BarType barType)
        => barType != BarType.A1;

    private static bool ShouldSampleRightSide(BarType barType)
        => barType != BarType.B1;

    private static double GetObjectScore(BlackSideBandSampleProfile profile, int index, BlackObjectRule rule)
    {
        int y = 255;

        if (profile.LeftValid[index])
        {
            y = Math.Min(y, profile.LeftY[index]);
        }

        if (profile.RightValid[index])
        {
            y = Math.Min(y, profile.RightY[index]);
        }

        return rule.MaximumObjectY - y;
    }

    private static int GetPercentileIndex(int count, double percentile)
    {
        double normalized = Math.Clamp(percentile, 0, 1);

        return RoundToInt((count - 1) * normalized);
    }

    private double GetObjectPercentile(bool hasTwoColoredTeamModels)
        => hasTwoColoredTeamModels
            ? _Options.TwoColoredTeamsObjectPercentile
            : _Options.OneColoredTeamObjectPercentile;

    private double GetSearchMinimumPercentile(bool hasTwoColoredTeamModels)
        => hasTwoColoredTeamModels
            ? _Options.TwoColoredTeamsSearchMinimumPercentile
            : _Options.OneColoredTeamSearchMinimumPercentile;

    private double GetSearchMaximumPercentile(bool hasTwoColoredTeamModels)
        => hasTwoColoredTeamModels
            ? _Options.TwoColoredTeamsSearchMaximumPercentile
            : _Options.OneColoredTeamSearchMaximumPercentile;

    private static int CalculateLocalOtsuThreshold(
        int[] histogram,
        int minimumY,
        int maximumY,
        int fallbackY)
    {
        int y0 = Math.Clamp(Math.Min(minimumY, maximumY), 0, histogram.Length - 1);
        int y1 = Math.Clamp(Math.Max(minimumY, maximumY), 0, histogram.Length - 1);

        if (y0 >= y1)
        {
            return fallbackY;
        }

        int totalWeight = 0;
        long totalSum = 0;

        for (int y = y0; y <= y1; y++)
        {
            int count = histogram[y];
            totalWeight += count;
            totalSum += (long)y * count;
        }

        if (totalWeight == 0)
        {
            return fallbackY;
        }

        int darkWeight = 0;
        long darkSum = 0;
        int bestThreshold = fallbackY;
        double bestScore = double.MinValue;

        for (int y = y0; y < y1; y++)
        {
            int count = histogram[y];
            darkWeight += count;
            darkSum += (long)y * count;

            int brightWeight = totalWeight - darkWeight;

            if (darkWeight == 0 ||
                brightWeight == 0)
            {
                continue;
            }

            double darkMean = (double)darkSum / darkWeight;
            double brightMean = (double)(totalSum - darkSum) / brightWeight;
            double meanDistance = darkMean - brightMean;
            double score = darkWeight * (double)brightWeight * meanDistance * meanDistance;

            if (score < bestScore ||
                (score == bestScore && Math.Abs(y - fallbackY) >= Math.Abs(bestThreshold - fallbackY)))
            {
                continue;
            }

            bestScore = score;
            bestThreshold = y;
        }

        return bestScore == double.MinValue
            ? fallbackY
            : bestThreshold;
    }

    private static int Median(int[] values, int count)
    {
        Array.Sort(values, 0, count);

        return values[count / 2];
    }

    private static bool IsInsideIgnoredMask(
        int x,
        int y,
        IReadOnlyList<RodObjectMask> ignoredMasks)
    {
        for (int rodIndex = 0; rodIndex < ignoredMasks.Count; rodIndex++)
        {
            var rod = ignoredMasks[rodIndex];

            for (int rectangleIndex = 0; rectangleIndex < rod.Rectangles.Count; rectangleIndex++)
            {
                var rectangle = rod.Rectangles[rectangleIndex];

                if (x >= rectangle.X &&
                    x < rectangle.RightExclusive &&
                    y >= rectangle.Y &&
                    y < rectangle.BottomExclusive)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInsideOcclusion(int x, int y, IReadOnlyList<Trapezium> occlusions)
    {
        for (int i = 0; i < occlusions.Count; i++)
        {
            if (IsInsideExpandedOcclusion(x, y, occlusions[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideExpandedOcclusion(int x, int y, Trapezium occlusion)
    {
        double upperY = GetYAtX(occlusion.UpperLeft, occlusion.UpperRight, x);
        double lowerY = GetYAtX(occlusion.LowerLeft, occlusion.LowerRight, x);
        double topY = Math.Min(upperY, lowerY);
        double bottomY = Math.Max(upperY, lowerY);
        double expansion = (bottomY - topY) * 0.5;

        return y >= topY - expansion &&
            y <= bottomY + expansion;
    }

    private static double GetYAtX(Point p0, Point p1, int x)
    {
        double dx = p1.X - p0.X;

        if (Math.Abs(dx) < 0.001)
        {
            return (p0.Y + p1.Y) * 0.5;
        }

        double t = (x - p0.X) / dx;

        return p0.Y + ((p1.Y - p0.Y) * t);
    }

    private static Rectangle CreateBounds(Trapezium trapezium, int width, int height)
    {
        int x0 = Convert.ToInt32(Math.Floor(Math.Min(
            Math.Min(trapezium.UpperLeft.X, trapezium.UpperRight.X),
            Math.Min(trapezium.LowerLeft.X, trapezium.LowerRight.X))));
        int y0 = Convert.ToInt32(Math.Floor(Math.Min(
            Math.Min(trapezium.UpperLeft.Y, trapezium.UpperRight.Y),
            Math.Min(trapezium.LowerLeft.Y, trapezium.LowerRight.Y))));
        int x1 = Convert.ToInt32(Math.Ceiling(Math.Max(
            Math.Max(trapezium.UpperLeft.X, trapezium.UpperRight.X),
            Math.Max(trapezium.LowerLeft.X, trapezium.LowerRight.X))));
        int y1 = Convert.ToInt32(Math.Ceiling(Math.Max(
            Math.Max(trapezium.UpperLeft.Y, trapezium.UpperRight.Y),
            Math.Max(trapezium.LowerLeft.Y, trapezium.LowerRight.Y))));

        return Rectangle.Intersect(
            new(x0, y0, x1 - x0 + 1, y1 - y0 + 1),
            new(0, 0, width, height));
    }

    private static Point Interpolate(Line line, double t)
        => new(
            line.P0.X + (line.Dx * t),
            line.P0.Y + (line.Dy * t));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetY(byte r, byte g, byte b)
        => ((77 * r) + (150 * g) + (29 * b)) >> 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundToInt(double value)
        => value >= 0
            ? (int)(value + 0.5)
            : (int)(value - 0.5);
}
