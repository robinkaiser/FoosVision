// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene.Processing.BlackObjects;

public unsafe class BlackRodObjectMaskDetector
{
    private readonly int _Width;
    private readonly int _Height;
    private readonly int _MaskMargin;
    private readonly int _AlongRodExpansionMargin;
    private readonly int _AlongRodAdaptiveExpansionStep;
    private readonly int _AlongRodAdaptiveExpansionEdgeDistance;
    private readonly int _AlongRodAdaptiveExpansionMaxExtra;
    private readonly int _CrossRodAllowedEmptyScans;
    private readonly int _MinimumColumnMatches;
    private readonly Rectangle[] _RectangleBuffer;
    private readonly RodBlackObjectMasks[] _Rods;
    private readonly Rectangle[][] _Rectangles;
    private readonly ReadOnlyBuffer<Rectangle>[] _RectangleLists;

    public BlackRodObjectMaskDetector(int width, int height, BlackRodObjectMaskDetectionOptions? options = null)
    {
        _Width = width;
        _Height = height;
        BlackRodObjectMaskDetectionOptions effectiveOptions = options ?? new BlackRodObjectMaskDetectionOptions();
        _MaskMargin = NormalizeNonNegative(effectiveOptions.MaskMargin);
        _AlongRodExpansionMargin = NormalizeNonNegative(effectiveOptions.AlongRodExpansionMargin);
        _AlongRodAdaptiveExpansionStep = NormalizeNonNegative(effectiveOptions.AlongRodAdaptiveExpansionStep);
        _AlongRodAdaptiveExpansionEdgeDistance = NormalizeNonNegative(effectiveOptions.AlongRodAdaptiveExpansionEdgeDistance);
        _AlongRodAdaptiveExpansionMaxExtra = NormalizeNonNegative(effectiveOptions.AlongRodAdaptiveExpansionMaxExtra);
        _CrossRodAllowedEmptyScans = NormalizeNonNegative(effectiveOptions.CrossRodAllowedEmptyScans);
        _MinimumColumnMatches = Math.Max(1, effectiveOptions.MinimumColumnMatches);
        _RectangleBuffer = new Rectangle[Math.Max(16, effectiveOptions.MaximumRectanglesPerRod)];
        _Rods = new RodBlackObjectMasks[8];
        _Rectangles = new Rectangle[8][];
        _RectangleLists = new ReadOnlyBuffer<Rectangle>[8];

        for (int i = 0; i < 8; i++)
        {
            _Rectangles[i] = new Rectangle[_RectangleBuffer.Length];
            _RectangleLists[i] = new(_Rectangles[i]);
        }
    }

    public BlackRodObjectMaskDetection Detect(
        byte[] frameBufferRGBA8888,
        PlayingField field,
        BlackRodObjectIntervalDetection intervalDetection)
    {
        int count = 0;

        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (Rectangle* pRectangles = _RectangleBuffer)
        {
            Rectangle fieldBounds = CreateBounds(field.Boundary, _Width, _Height);

            DetectFieldBar(
                pFrame,
                pRectangles,
                ref count,
                field.Bars.A1,
                null,
                field.Bars.A2,
                intervalDetection.Rods[0],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBar(
                pFrame,
                pRectangles,
                ref count,
                field.Bars.A2,
                field.Bars.A1,
                field.Bars.B3,
                intervalDetection.Rods[1],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBar(
                pFrame,
                pRectangles,
                ref count,
                field.Bars.B3,
                field.Bars.A2,
                field.Bars.A5,
                intervalDetection.Rods[2],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBar(
                pFrame,
                pRectangles,
                ref count,
                field.Bars.A5,
                field.Bars.B3,
                field.Bars.B5,
                intervalDetection.Rods[3],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBar(
                pFrame,
                pRectangles,
                ref count,
                field.Bars.B5,
                field.Bars.A5,
                field.Bars.A3,
                intervalDetection.Rods[4],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBar(
                pFrame,
                pRectangles,
                ref count,
                field.Bars.A3,
                field.Bars.B5,
                field.Bars.B2,
                intervalDetection.Rods[5],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBar(
                pFrame,
                pRectangles,
                ref count,
                field.Bars.B2,
                field.Bars.A3,
                field.Bars.B1,
                intervalDetection.Rods[6],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBar(
                pFrame,
                pRectangles,
                ref count,
                field.Bars.B1,
                field.Bars.B2,
                null,
                intervalDetection.Rods[7],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
        }

        return new(_Rods);
    }

    public RodBlackObjectMasks DetectBar(
        byte[] frameBufferRGBA8888,
        Bar bar,
        RodBlackObjectIntervals intervals,
        BlackObjectRule rule)
    {
        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (Rectangle* pRectangles = _RectangleBuffer)
            return DetectBar(
                pFrame,
                pRectangles,
                _RectangleBuffer.Length,
                bar,
                null,
                null,
                intervals,
                rule,
                new(0, 0, _Width, _Height),
                [],
                0);
    }

    public int DetectRectangles(
        byte[] frameBufferRGBA8888,
        PlayingField field,
        BlackRodObjectIntervalDetection intervalDetection,
        Rectangle[] outRectangles,
        RodBlackObjectMaskRange[] outRodRanges)
    {
        if (outRodRanges.Length < 8)
        {
            throw new ArgumentException("The output rod range buffer must have room for all eight rods.", nameof(outRodRanges));
        }

        int rectangleCount = 0;
        int rodCount = 0;

        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (Rectangle* pRectangles = outRectangles)
        {
            Rectangle fieldBounds = CreateBounds(field.Boundary, _Width, _Height);

            DetectFieldBarRectangles(
                pFrame,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.A1,
                null,
                field.Bars.A2,
                intervalDetection.Rods[0],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBarRectangles(
                pFrame,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.A2,
                field.Bars.A1,
                field.Bars.B3,
                intervalDetection.Rods[1],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBarRectangles(
                pFrame,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.B3,
                field.Bars.A2,
                field.Bars.A5,
                intervalDetection.Rods[2],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBarRectangles(
                pFrame,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.A5,
                field.Bars.B3,
                field.Bars.B5,
                intervalDetection.Rods[3],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBarRectangles(
                pFrame,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.B5,
                field.Bars.A5,
                field.Bars.A3,
                intervalDetection.Rods[4],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBarRectangles(
                pFrame,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.A3,
                field.Bars.B5,
                field.Bars.B2,
                intervalDetection.Rods[5],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBarRectangles(
                pFrame,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.B2,
                field.Bars.A3,
                field.Bars.B1,
                intervalDetection.Rods[6],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
            DetectFieldBarRectangles(
                pFrame,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.B1,
                field.Bars.B2,
                null,
                intervalDetection.Rods[7],
                intervalDetection.Rule,
                fieldBounds,
                field.Occlusions);
        }

        return rectangleCount;
    }

    private void DetectFieldBar(
        byte* pFrame,
        Rectangle* pRectangles,
        ref int count,
        Bar bar,
        Bar? leftNeighborBar,
        Bar? rightNeighborBar,
        RodBlackObjectIntervals intervals,
        BlackObjectRule rule,
        Rectangle clipRectangle,
        IReadOnlyList<Trapezium> occlusions)
    {
        _Rods[count] = DetectBar(
            pFrame,
            pRectangles,
            _RectangleBuffer.Length,
            bar,
            leftNeighborBar,
            rightNeighborBar,
            intervals,
            rule,
            clipRectangle,
            occlusions,
            count);
        count++;
    }

    private void DetectFieldBarRectangles(
        byte* pFrame,
        Rectangle* pRectangles,
        int maxRectangles,
        RodBlackObjectMaskRange[] rodRanges,
        ref int rodCount,
        ref int rectangleCount,
        Bar bar,
        Bar? leftNeighborBar,
        Bar? rightNeighborBar,
        RodBlackObjectIntervals intervals,
        BlackObjectRule rule,
        Rectangle clipRectangle,
        IReadOnlyList<Trapezium> occlusions)
    {
        int startIndex = rectangleCount;
        int count = DetectBarRectangles(
            pFrame,
            pRectangles + startIndex,
            maxRectangles - startIndex,
            bar,
            leftNeighborBar,
            rightNeighborBar,
            intervals,
            rule,
            clipRectangle,
            occlusions);

        rodRanges[rodCount] = new(bar.Type, startIndex, count);
        rodCount++;
        rectangleCount += count;
    }

    private RodBlackObjectMasks DetectBar(
        byte* pFrame,
        Rectangle* pRectangles,
        int maxRectangles,
        Bar bar,
        Bar? leftNeighborBar,
        Bar? rightNeighborBar,
        RodBlackObjectIntervals intervals,
        BlackObjectRule rule,
        Rectangle clipRectangle,
        IReadOnlyList<Trapezium> occlusions,
        int bufferIndex)
    {
        int rectangleCount = DetectBarRectangles(
            pFrame,
            pRectangles,
            maxRectangles,
            bar,
            leftNeighborBar,
            rightNeighborBar,
            intervals,
            rule,
            clipRectangle,
            occlusions);

        for (int i = 0; i < rectangleCount; i++)
        {
            _Rectangles[bufferIndex][i] = pRectangles[i];
        }

        _RectangleLists[bufferIndex].SetCount(rectangleCount);

        return new(bar.Type, _RectangleLists[bufferIndex]);
    }

    private int DetectBarRectangles(
        byte* pFrame,
        Rectangle* pRectangles,
        int maxRectangles,
        Bar bar,
        Bar? leftNeighborBar,
        Bar? rightNeighborBar,
        RodBlackObjectIntervals intervals,
        BlackObjectRule rule,
        Rectangle clipRectangle,
        IReadOnlyList<Trapezium> occlusions)
    {
        int outCount = 0;
        BlackSideBandSampleProfile profile = intervals.SampleProfile;
        RodExpansionBounds expansionBounds = CreateExpansionBounds(bar, leftNeighborBar, rightNeighborBar, clipRectangle);

        for (int i = 0; i < intervals.Intervals.Count; i++)
        {
            Rectangle rectangle = CreateStartRectangle(bar, profile, intervals.Intervals[i]);
            Rectangle expansionClipRectangle = CreateExpansionClipRectangle(rectangle, expansionBounds, clipRectangle);
            rectangle = ExpandCrossRod(pFrame, rectangle, expansionClipRectangle, rule, occlusions);

            int margin = _MaskMargin;
            rectangle = new(
                rectangle.X - margin,
                rectangle.Y - margin,
                rectangle.Width + (margin * 2),
                rectangle.Height + (margin * 2));
            rectangle = Rectangle.Intersect(rectangle, clipRectangle);
            rectangle = Rectangle.Intersect(rectangle, expansionClipRectangle);

            if (rectangle.IsEmpty)
            {
                continue;
            }

            if (outCount >= maxRectangles)
            {
                return outCount;
            }

            pRectangles[outCount] = rectangle;
            outCount++;
        }

        return outCount;
    }

    private Rectangle CreateStartRectangle(
        Bar bar,
        BlackSideBandSampleProfile profile,
        RodObjectInterval interval)
    {
        int startIndex = Math.Clamp(interval.StartIndex, 0, profile.Count - 1);
        int endIndex = Math.Clamp(interval.EndIndex, 0, profile.Count - 1);
        double t0 = profile.Count <= 1 ? 0 : (double)startIndex / (profile.Count - 1);
        double t1 = profile.Count <= 1 ? 0 : (double)endIndex / (profile.Count - 1);
        Point left0 = Interpolate(bar.Left, t0);
        Point left1 = Interpolate(bar.Left, t1);
        Point right0 = Interpolate(bar.Right, t0);
        Point right1 = Interpolate(bar.Right, t1);
        int x0 = FloorToInt(Math.Min(
            Math.Min(left0.X, left1.X),
            Math.Min(right0.X, right1.X)));
        int x1 = CeilingToInt(Math.Max(
            Math.Max(left0.X, left1.X),
            Math.Max(right0.X, right1.X)));
        int y0 = Math.Min(profile.Y[startIndex], profile.Y[endIndex]) - _AlongRodExpansionMargin;
        int y1 = Math.Max(profile.Y[startIndex], profile.Y[endIndex]) + _AlongRodExpansionMargin;

        return new(
            x0,
            y0,
            x1 - x0 + 1,
            y1 - y0 + 1);
    }

    private static RodExpansionBounds CreateExpansionBounds(
        Bar bar,
        Bar? leftNeighborBar,
        Bar? rightNeighborBar,
        Rectangle clipRectangle)
    {
        double centerX = GetCenterX(bar);
        int maximumLeftPixels = leftNeighborBar is null
            ? RoundToInt(centerX - clipRectangle.X)
            : RoundToInt(Math.Abs(centerX - GetCenterX(leftNeighborBar)) * 0.5);
        int maximumRightPixels = rightNeighborBar is null
            ? RoundToInt(clipRectangle.RightExclusive - 1 - centerX)
            : RoundToInt(Math.Abs(GetCenterX(rightNeighborBar) - centerX) * 0.5);

        return new(
            Math.Max(0, maximumLeftPixels),
            Math.Max(0, maximumRightPixels));
    }

    private static Rectangle CreateExpansionClipRectangle(
        Rectangle startRectangle,
        RodExpansionBounds bounds,
        Rectangle clipRectangle)
    {
        int x0 = Math.Max(clipRectangle.X, startRectangle.X - bounds.MaximumLeftPixels);
        int x1 = Math.Min(clipRectangle.RightExclusive - 1, startRectangle.RightExclusive - 1 + bounds.MaximumRightPixels);

        return new(
            x0,
            clipRectangle.Y,
            x1 - x0 + 1,
            clipRectangle.Height);
    }

    private Rectangle ExpandCrossRod(
        byte* pFrame,
        Rectangle rectangle,
        Rectangle clipRectangle,
        BlackObjectRule rule,
        IReadOnlyList<Trapezium> occlusions)
    {
        rectangle = Rectangle.Intersect(rectangle, clipRectangle);

        if (rectangle.IsEmpty)
        {
            return rectangle;
        }

        int leftY0 = rectangle.Y;
        int leftY1Exclusive = rectangle.BottomExclusive;
        int rightY0 = rectangle.Y;
        int rightY1Exclusive = rectangle.BottomExclusive;
        int x0 = ExpandLeft(
            pFrame,
            rectangle.X,
            rectangle.Y,
            rectangle.BottomExclusive,
            clipRectangle.Y,
            clipRectangle.BottomExclusive,
            clipRectangle.X,
            rule,
            occlusions,
            ref leftY0,
            ref leftY1Exclusive);
        int x1 = ExpandRight(
            pFrame,
            rectangle.RightExclusive - 1,
            rectangle.Y,
            rectangle.BottomExclusive,
            clipRectangle.Y,
            clipRectangle.BottomExclusive,
            clipRectangle.RightExclusive - 1,
            rule,
            occlusions,
            ref rightY0,
            ref rightY1Exclusive);
        int y0 = Math.Min(rectangle.Y, Math.Min(leftY0, rightY0));
        int y1Exclusive = Math.Max(rectangle.BottomExclusive, Math.Max(leftY1Exclusive, rightY1Exclusive));

        return new(
            x0,
            y0,
            x1 - x0 + 1,
            y1Exclusive - y0);
    }

    private int ExpandLeft(
        byte* pFrame,
        int startX,
        int y0,
        int y1Exclusive,
        int clipY0,
        int clipY1Exclusive,
        int minimumX,
        BlackObjectRule rule,
        IReadOnlyList<Trapezium> occlusions,
        ref int matchedY0,
        ref int matchedY1Exclusive)
    {
        int bestX = startX;
        int missCount = 0;
        int scanY0 = y0;
        int scanY1Exclusive = y1Exclusive;

        for (int x = startX - 1; x >= minimumX; x--)
        {
            if (TryFindColumnMatch(
                pFrame,
                x,
                scanY0,
                scanY1Exclusive,
                rule,
                occlusions,
                out int columnY0,
                out int columnY1Exclusive))
            {
                bestX = x;
                missCount = 0;
                matchedY0 = Math.Min(matchedY0, columnY0);
                matchedY1Exclusive = Math.Max(matchedY1Exclusive, columnY1Exclusive);
                ExpandAlongRodSearchWindow(
                    y0,
                    y1Exclusive,
                    clipY0,
                    clipY1Exclusive,
                    columnY0,
                    columnY1Exclusive,
                    ref scanY0,
                    ref scanY1Exclusive);
                continue;
            }

            missCount++;

            if (missCount > _CrossRodAllowedEmptyScans)
            {
                break;
            }
        }

        return bestX;
    }

    private int ExpandRight(
        byte* pFrame,
        int startX,
        int y0,
        int y1Exclusive,
        int clipY0,
        int clipY1Exclusive,
        int maximumX,
        BlackObjectRule rule,
        IReadOnlyList<Trapezium> occlusions,
        ref int matchedY0,
        ref int matchedY1Exclusive)
    {
        int bestX = startX;
        int missCount = 0;
        int scanY0 = y0;
        int scanY1Exclusive = y1Exclusive;

        for (int x = startX + 1; x <= maximumX; x++)
        {
            if (TryFindColumnMatch(
                pFrame,
                x,
                scanY0,
                scanY1Exclusive,
                rule,
                occlusions,
                out int columnY0,
                out int columnY1Exclusive))
            {
                bestX = x;
                missCount = 0;
                matchedY0 = Math.Min(matchedY0, columnY0);
                matchedY1Exclusive = Math.Max(matchedY1Exclusive, columnY1Exclusive);
                ExpandAlongRodSearchWindow(
                    y0,
                    y1Exclusive,
                    clipY0,
                    clipY1Exclusive,
                    columnY0,
                    columnY1Exclusive,
                    ref scanY0,
                    ref scanY1Exclusive);
                continue;
            }

            missCount++;

            if (missCount > _CrossRodAllowedEmptyScans)
            {
                break;
            }
        }

        return bestX;
    }

    private bool TryFindColumnMatch(
        byte* pFrame,
        int x,
        int y0,
        int y1Exclusive,
        BlackObjectRule rule,
        IReadOnlyList<Trapezium> occlusions,
        out int matchedY0,
        out int matchedY1Exclusive)
    {
        int matchCount = 0;
        matchedY0 = int.MaxValue;
        matchedY1Exclusive = int.MinValue;

        for (int y = y0; y < y1Exclusive; y++)
        {
            if (IsInsideOcclusion(x, y, occlusions) ||
                !PixelMatches(pFrame, x, y, rule))
            {
                continue;
            }

            matchCount++;
            matchedY0 = Math.Min(matchedY0, y);
            matchedY1Exclusive = Math.Max(matchedY1Exclusive, y + 1);
        }

        return matchCount >= _MinimumColumnMatches;
    }

    private void ExpandAlongRodSearchWindow(
        int initialY0,
        int initialY1Exclusive,
        int clipY0,
        int clipY1Exclusive,
        int matchedY0,
        int matchedY1Exclusive,
        ref int scanY0,
        ref int scanY1Exclusive)
    {
        int maxExtra = _AlongRodAdaptiveExpansionMaxExtra;
        int step = _AlongRodAdaptiveExpansionStep;

        if (maxExtra == 0 ||
            step == 0)
        {
            return;
        }

        int edgeDistance = _AlongRodAdaptiveExpansionEdgeDistance;

        if (matchedY0 <= scanY0 + edgeDistance)
        {
            scanY0 = Math.Max(Math.Max(clipY0, initialY0 - maxExtra), scanY0 - step);
        }

        if (matchedY1Exclusive >= scanY1Exclusive - edgeDistance)
        {
            scanY1Exclusive = Math.Min(Math.Min(clipY1Exclusive, initialY1Exclusive + maxExtra), scanY1Exclusive + step);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool PixelMatches(byte* pFrame, int x, int y, BlackObjectRule rule)
    {
        byte* pPixel = pFrame + (((y * _Width) + x) * 4);

        return GetY(pPixel[0], pPixel[1], pPixel[2]) <= rule.MaximumObjectY;
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

    private static double GetCenterX(Bar bar)
        => (bar.Center.P0.X + bar.Center.P1.X) * 0.5;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetY(byte r, byte g, byte b)
        => ((77 * r) + (150 * g) + (29 * b)) >> 8;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundToInt(double value)
        => value >= 0
            ? (int)(value + 0.5)
            : (int)(value - 0.5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int FloorToInt(double value)
    {
        int truncated = (int)value;

        return value < truncated ? truncated - 1 : truncated;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CeilingToInt(double value)
    {
        int truncated = (int)value;

        return value > truncated ? truncated + 1 : truncated;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NormalizeNonNegative(int value)
        => value < 0 ? 0 : value;

    private readonly record struct RodExpansionBounds(int MaximumLeftPixels, int MaximumRightPixels);
}

public record BlackRodObjectMaskDetectionOptions(
    int MaskMargin = 5,
    int AlongRodExpansionMargin = 5,
    int AlongRodAdaptiveExpansionStep = 2,
    int AlongRodAdaptiveExpansionEdgeDistance = 1,
    int AlongRodAdaptiveExpansionMaxExtra = 16,
    int CrossRodAllowedEmptyScans = 2,
    int MinimumColumnMatches = 1,
    int MaximumRectanglesPerRod = 128);
