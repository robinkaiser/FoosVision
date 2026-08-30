// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public unsafe class ColoredPlayerMaskDetector
{
    private readonly int _Width;
    private readonly int _Height;
    private readonly double _RodColorModelRadiusScale;
    private readonly double _ExpansionColorModelRadiusScale;
    private readonly int _MaskMargin;
    private readonly int _AlongRodExpansionMargin;
    private readonly int _AlongRodAdaptiveExpansionStep;
    private readonly int _AlongRodAdaptiveExpansionEdgeDistance;
    private readonly int _AlongRodAdaptiveExpansionMaxExtra;
    private readonly int _AlongRodAllowedMisses;
    private readonly int _CrossRodAllowedEmptyScans;
    private readonly int _MinimumCrossSectionMatches;
    private readonly BPoint[] _CenterPoints;
    private readonly BPoint[] _CrossSectionPoints;
    private readonly Rectangle[] _RectangleBuffer;
    private readonly RodColoredPlayerMasks[] _Rods;
    private readonly Rectangle[][] _Rectangles;
    private readonly ReadOnlyBuffer<Rectangle>[] _RectangleLists;

    public ColoredPlayerMaskDetector(int width, int height, ColoredPlayerMaskDetectionOptions? options = null)
    {
        _Width = width;
        _Height = height;
        ColoredPlayerMaskDetectionOptions effectiveOptions = options ?? new ColoredPlayerMaskDetectionOptions();
        _RodColorModelRadiusScale = NormalizeNonNegative(effectiveOptions.RodColorModelRadiusScale);
        _ExpansionColorModelRadiusScale = NormalizeNonNegative(effectiveOptions.ExpansionColorModelRadiusScale);
        _MaskMargin = effectiveOptions.MaskMargin;
        _AlongRodExpansionMargin = effectiveOptions.AlongRodExpansionMargin;
        _AlongRodAdaptiveExpansionStep = NormalizeNonNegative(effectiveOptions.AlongRodAdaptiveExpansionStep);
        _AlongRodAdaptiveExpansionEdgeDistance = NormalizeNonNegative(effectiveOptions.AlongRodAdaptiveExpansionEdgeDistance);
        _AlongRodAdaptiveExpansionMaxExtra = NormalizeNonNegative(effectiveOptions.AlongRodAdaptiveExpansionMaxExtra);
        _AlongRodAllowedMisses = effectiveOptions.AlongRodAllowedMisses;
        _CrossRodAllowedEmptyScans = effectiveOptions.CrossRodAllowedEmptyScans;
        _MinimumCrossSectionMatches = effectiveOptions.MinimumCrossSectionMatches;

        int lineCapacity = Math.Max(width, height);
        _CenterPoints = new BPoint[lineCapacity];
        _CrossSectionPoints = new BPoint[width + height];
        _RectangleBuffer = new Rectangle[Math.Max(16, effectiveOptions.MaximumRectanglesPerRod)];
        _Rods = new RodColoredPlayerMasks[8];
        _Rectangles = new Rectangle[8][];
        _RectangleLists = new ReadOnlyBuffer<Rectangle>[8];

        for (int i = 0; i < 8; i++)
        {
            _Rectangles[i] = new Rectangle[_RectangleBuffer.Length];
            _RectangleLists[i] = new(_Rectangles[i]);
        }
    }

    public ColoredPlayerMaskDetection Detect(
        byte[] frameBufferRGBA8888,
        PlayingField field,
        ColoredPlayerColorCalibration calibration)
    {
        int count = 0;

        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (BPoint* pCenterPoints = _CenterPoints)
        fixed (BPoint* pCrossSectionPoints = _CrossSectionPoints)
        fixed (Rectangle* pRectangles = _RectangleBuffer)
        {
            Rectangle fieldBounds = CreateBounds(field.Boundary, _Width, _Height);

            DetectFieldBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                ref count,
                field.Bars.A1,
                fieldBounds,
                calibration);
            DetectFieldBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                ref count,
                field.Bars.A2,
                fieldBounds,
                calibration);
            DetectFieldBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                ref count,
                field.Bars.B3,
                fieldBounds,
                calibration);
            DetectFieldBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                ref count,
                field.Bars.A5,
                fieldBounds,
                calibration);
            DetectFieldBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                ref count,
                field.Bars.B5,
                fieldBounds,
                calibration);
            DetectFieldBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                ref count,
                field.Bars.A3,
                fieldBounds,
                calibration);
            DetectFieldBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                ref count,
                field.Bars.B2,
                fieldBounds,
                calibration);
            DetectFieldBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                ref count,
                field.Bars.B1,
                fieldBounds,
                calibration);
        }

        return new(_Rods);
    }

    public RodColoredPlayerMasks DetectBar(
        byte[] frameBufferRGBA8888,
        Bar bar,
        ColoredPlayerColorCalibration calibration)
    {
        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (BPoint* pCenterPoints = _CenterPoints)
        fixed (BPoint* pCrossSectionPoints = _CrossSectionPoints)
        fixed (Rectangle* pRectangles = _RectangleBuffer)
            return DetectBar(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                _RectangleBuffer.Length,
                bar,
                new(0, 0, _Width, _Height),
                calibration,
                0);
    }

    public int DetectBarRectangles(
        byte[] frameBufferRGBA8888,
        Bar bar,
        ColoredPlayerColorCalibration calibration,
        int outBufferStartPos,
        Rectangle[] outRectangles)
    {
        if (outBufferStartPos >= outRectangles.Length)
        {
            return 0;
        }

        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (BPoint* pCenterPoints = _CenterPoints)
        fixed (BPoint* pCrossSectionPoints = _CrossSectionPoints)
        fixed (Rectangle* pRectangles = outRectangles)
            return DetectBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles + outBufferStartPos,
                outRectangles.Length - outBufferStartPos,
                bar,
                new(0, 0, _Width, _Height),
                calibration);
    }

    public int DetectRectangles(
        byte[] frameBufferRGBA8888,
        PlayingField field,
        ColoredPlayerColorCalibration calibration,
        Rectangle[] outRectangles,
        RodColoredPlayerMaskRange[] outRodRanges)
    {
        if (outRodRanges.Length < 8)
        {
            throw new ArgumentException("The output rod range buffer must have room for all eight rods.", nameof(outRodRanges));
        }

        int rectangleCount = 0;
        int rodCount = 0;

        fixed (byte* pFrame = frameBufferRGBA8888)
        fixed (BPoint* pCenterPoints = _CenterPoints)
        fixed (BPoint* pCrossSectionPoints = _CrossSectionPoints)
        fixed (Rectangle* pRectangles = outRectangles)
        {
            Rectangle fieldBounds = CreateBounds(field.Boundary, _Width, _Height);

            DetectFieldBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.A1,
                fieldBounds,
                calibration);
            DetectFieldBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.A2,
                fieldBounds,
                calibration);
            DetectFieldBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.B3,
                fieldBounds,
                calibration);
            DetectFieldBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.A5,
                fieldBounds,
                calibration);
            DetectFieldBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.B5,
                fieldBounds,
                calibration);
            DetectFieldBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.A3,
                fieldBounds,
                calibration);
            DetectFieldBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.B2,
                fieldBounds,
                calibration);
            DetectFieldBarRectangles(
                pFrame,
                pCenterPoints,
                pCrossSectionPoints,
                pRectangles,
                outRectangles.Length,
                outRodRanges,
                ref rodCount,
                ref rectangleCount,
                field.Bars.B1,
                fieldBounds,
                calibration);
        }

        return rectangleCount;
    }

    private void DetectFieldBar(
        byte* pFrame,
        BPoint* pCenterPoints,
        BPoint* pCrossSectionPoints,
        Rectangle* pRectangles,
        ref int count,
        Bar bar,
        Rectangle clipRectangle,
        ColoredPlayerColorCalibration calibration)
    {
        _Rods[count] = DetectBar(
            pFrame,
            pCenterPoints,
            pCrossSectionPoints,
            pRectangles,
            _RectangleBuffer.Length,
            bar,
            clipRectangle,
            calibration,
            count);
        count++;
    }

    private void DetectFieldBarRectangles(
        byte* pFrame,
        BPoint* pCenterPoints,
        BPoint* pCrossSectionPoints,
        Rectangle* pRectangles,
        int maxRectangles,
        RodColoredPlayerMaskRange[] rodRanges,
        ref int rodCount,
        ref int rectangleCount,
        Bar bar,
        Rectangle clipRectangle,
        ColoredPlayerColorCalibration calibration)
    {
        int startIndex = rectangleCount;
        int count = DetectBarRectangles(
            pFrame,
            pCenterPoints,
            pCrossSectionPoints,
            pRectangles + startIndex,
            maxRectangles - startIndex,
            bar,
            clipRectangle,
            calibration);

        rodRanges[rodCount] = new(bar.Type, startIndex, count);
        rodCount++;
        rectangleCount += count;
    }

    private RodColoredPlayerMasks DetectBar(
        byte* pFrame,
        BPoint* pCenterPoints,
        BPoint* pCrossSectionPoints,
        Rectangle* pRectangles,
        int maxRectangles,
        Bar bar,
        Rectangle clipRectangle,
        ColoredPlayerColorCalibration calibration,
        int bufferIndex)
    {
        int rectangleCount = DetectBarRectangles(
            pFrame,
            pCenterPoints,
            pCrossSectionPoints,
            pRectangles,
            maxRectangles,
            bar,
            clipRectangle,
            calibration);

        for (int i = 0; i < rectangleCount; i++)
        {
            _Rectangles[bufferIndex][i] = pRectangles[i];
        }

        _RectangleLists[bufferIndex].SetCount(rectangleCount);

        return new(bar.Type, _RectangleLists[bufferIndex]);
    }

    private int DetectBarRectangles(
        byte* pFrame,
        BPoint* pCenterPoints,
        BPoint* pCrossSectionPoints,
        Rectangle* pRectangles,
        int maxRectangles,
        Bar bar,
        Rectangle clipRectangle,
        ColoredPlayerColorCalibration calibration)
    {
        ChromaticColorModel? model = GetColorModel(bar.Type, calibration);

        if (model is null)
        {
            return 0;
        }

        int x0 = RoundToInt(bar.Center.P0.X);
        int y0 = RoundToInt(bar.Center.P0.Y);
        int x1 = RoundToInt(bar.Center.P1.X);
        int y1 = RoundToInt(bar.Center.P1.Y);
        int centerPointCount = Bresenham.GetPoints(new(new(x0, y0), new(x1, y1)), pCenterPoints);

        if (centerPointCount == 0)
        {
            return 0;
        }

        return FindRectangles(
            pFrame,
            pCrossSectionPoints,
            pRectangles,
            maxRectangles,
            centerPointCount,
            bar,
            clipRectangle,
            model);
    }

    private int FindRectangles(
        byte* pFrame,
        BPoint* pCrossSectionPoints,
        Rectangle* pRectangles,
        int maxRectangles,
        int centerPointCount,
        Bar bar,
        Rectangle clipRectangle,
        ChromaticColorModel model)
    {
        int outCount = 0;
        bool isActiveRectangle = false;
        int missCount = 0;
        int activeX0 = 0;
        int activeY0 = 0;
        int activeX1 = 0;
        int activeY1 = 0;

        int rodRadiusSquared = ScaleRadiusSquared(model.RadiusSquared, _RodColorModelRadiusScale);
        int expansionRadiusSquared = ScaleRadiusSquared(model.RadiusSquared, _ExpansionColorModelRadiusScale);
        int minimumChromaticDistanceSquared = model.MinimumChromaticDistance * model.MinimumChromaticDistance;

        for (int i = 0; i < centerPointCount; i++)
        {
            double t = centerPointCount == 1 ? 0 : (double)i / (centerPointCount - 1);
            CrossSectionSegment rodCrossSection = CreateRodCrossSection(bar, t);

            bool hasMatch = TryFindRodCrossSectionMatch(
                pFrame,
                pCrossSectionPoints,
                rodCrossSection,
                model.CenterCb,
                model.CenterCr,
                rodRadiusSquared,
                minimumChromaticDistanceSquared,
                out Rectangle lineRectangle);

            if (!hasMatch)
            {
                if (!isActiveRectangle)
                {
                    continue;
                }

                missCount++;

                if (missCount <= _AlongRodAllowedMisses)
                {
                    continue;
                }

                AddExpandedMaskRectangle(
                    pFrame,
                    pRectangles,
                    maxRectangles,
                    clipRectangle,
                    model.CenterCb,
                    model.CenterCr,
                    expansionRadiusSquared,
                    minimumChromaticDistanceSquared,
                    ref outCount,
                    activeX0,
                    activeY0,
                    activeX1,
                    activeY1);
                isActiveRectangle = false;
                missCount = 0;
                continue;
            }

            missCount = 0;

            if (!isActiveRectangle)
            {
                isActiveRectangle = true;
                activeX0 = lineRectangle.X;
                activeY0 = lineRectangle.Y;
                activeX1 = lineRectangle.RightExclusive - 1;
                activeY1 = lineRectangle.BottomExclusive - 1;
                continue;
            }

            activeX0 = Math.Min(activeX0, lineRectangle.X);
            activeY0 = Math.Min(activeY0, lineRectangle.Y);
            activeX1 = Math.Max(activeX1, lineRectangle.RightExclusive - 1);
            activeY1 = Math.Max(activeY1, lineRectangle.BottomExclusive - 1);
        }

        if (isActiveRectangle)
        {
            AddExpandedMaskRectangle(
                pFrame,
                pRectangles,
                maxRectangles,
                clipRectangle,
                model.CenterCb,
                model.CenterCr,
                expansionRadiusSquared,
                minimumChromaticDistanceSquared,
                ref outCount,
                activeX0,
                activeY0,
                activeX1,
                activeY1);
        }

        return outCount;
    }

    private bool TryFindRodCrossSectionMatch(
        byte* pFrame,
        BPoint* pCrossSectionPoints,
        CrossSectionSegment crossSection,
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared,
        out Rectangle rectangle)
    {
        BLine line = new(new(crossSection.X0, crossSection.Y0), new(crossSection.X1, crossSection.Y1));
        int pointCount = Bresenham.GetPoints(line, pCrossSectionPoints);
        int matchCount = 0;
        int x0 = int.MaxValue;
        int y0 = int.MaxValue;
        int x1 = int.MinValue;
        int y1 = int.MinValue;

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
            ColorFeature feature = ColorFeature.FromRgb(pPixel[0], pPixel[1], pPixel[2]);

            if (!MatchesModel(
                feature.Cb,
                feature.Cr,
                centerCb,
                centerCr,
                radiusSquared,
                minimumChromaticDistanceSquared))
            {
                continue;
            }

            matchCount++;
            x0 = Math.Min(x0, x);
            y0 = Math.Min(y0, y);
            x1 = Math.Max(x1, x);
            y1 = Math.Max(y1, y);
        }

        if (matchCount < _MinimumCrossSectionMatches)
        {
            rectangle = default;
            return false;
        }

        rectangle = new(
            x0,
            y0,
            x1 - x0 + 1,
            y1 - y0 + 1);

        return true;
    }

    private void AddExpandedMaskRectangle(
        byte* pFrame,
        Rectangle* pRectangles,
        int maxRectangles,
        Rectangle clipRectangle,
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared,
        ref int outCount,
        int x0,
        int y0,
        int x1,
        int y1)
    {
        if (outCount >= maxRectangles)
        {
            return;
        }

        Rectangle rectangle = CreateExpandedStartRectangle(x0, y0, x1, y1);
        rectangle = ExpandCrossRod(
            pFrame,
            rectangle,
            clipRectangle,
            centerCb,
            centerCr,
            radiusSquared,
            minimumChromaticDistanceSquared);

        int margin = _MaskMargin;
        rectangle = new(
            rectangle.X - margin,
            rectangle.Y - margin,
            rectangle.Width + (margin * 2),
            rectangle.Height + (margin * 2));

        rectangle = Rectangle.Intersect(rectangle, clipRectangle);

        if (rectangle.IsEmpty)
        {
            return;
        }

        pRectangles[outCount] = rectangle;
        outCount++;
    }

    private Rectangle CreateExpandedStartRectangle(int x0, int y0, int x1, int y1)
    {
        int margin = _AlongRodExpansionMargin;

        y0 -= margin;
        y1 += margin;

        return new(
            x0,
            y0,
            x1 - x0 + 1,
            y1 - y0 + 1);
    }

    private Rectangle ExpandCrossRod(
        byte* pFrame,
        Rectangle rectangle,
        Rectangle clipRectangle,
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared)
    {
        rectangle = Rectangle.Intersect(rectangle, clipRectangle);

        if (rectangle.IsEmpty)
        {
            return rectangle;
        }

        return ExpandColumns(
            pFrame,
            rectangle,
            clipRectangle,
            centerCb,
            centerCr,
            radiusSquared,
            minimumChromaticDistanceSquared);
    }

    private static CrossSectionSegment CreateRodCrossSection(Bar bar, double t)
    {
        Point left = Interpolate(bar.Left, t);
        Point right = Interpolate(bar.Right, t);

        return new(
            RoundToInt(left.X),
            RoundToInt(left.Y),
            RoundToInt(right.X),
            RoundToInt(right.Y));
    }

    private Rectangle ExpandColumns(
        byte* pFrame,
        Rectangle rectangle,
        Rectangle clipRectangle,
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared)
    {
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
            centerCb,
            centerCr,
            radiusSquared,
            minimumChromaticDistanceSquared,
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
            centerCb,
            centerCr,
            radiusSquared,
            minimumChromaticDistanceSquared,
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
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared,
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
                centerCb,
                centerCr,
                radiusSquared,
                minimumChromaticDistanceSquared,
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
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared,
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
                centerCb,
                centerCr,
                radiusSquared,
                minimumChromaticDistanceSquared,
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
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared,
        out int matchedY0,
        out int matchedY1Exclusive)
    {
        bool hasMatch = false;
        matchedY0 = int.MaxValue;
        matchedY1Exclusive = int.MinValue;

        for (int y = y0; y < y1Exclusive; y++)
        {
            if (PixelMatches(pFrame, x, y, centerCb, centerCr, radiusSquared, minimumChromaticDistanceSquared))
            {
                hasMatch = true;
                matchedY0 = Math.Min(matchedY0, y);
                matchedY1Exclusive = Math.Max(matchedY1Exclusive, y + 1);
            }
        }

        return hasMatch;
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
    private bool PixelMatches(
        byte* pFrame,
        int x,
        int y,
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared)
    {
        byte* pPixel = pFrame + (((y * _Width) + x) * 4);
        ColorFeature feature = ColorFeature.FromRgb(pPixel[0], pPixel[1], pPixel[2]);

        return MatchesModel(
            feature.Cb,
            feature.Cr,
            centerCb,
            centerCr,
            radiusSquared,
            minimumChromaticDistanceSquared);
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

    private static ChromaticColorModel? GetColorModel(
        BarType barType,
        ColoredPlayerColorCalibration calibration)
    {
        return TableBarClassifier.GetTeam(barType) switch
        {
            Team.A => calibration.TeamA.ColorModel,
            Team.B => calibration.TeamB.ColorModel,
            _ => null,
        };
    }

    private static Point Interpolate(Line line, double t)
        => new(
            line.P0.X + (line.Dx * t),
            line.P0.Y + (line.Dy * t));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesModel(
        int cb,
        int cr,
        int centerCb,
        int centerCr,
        int radiusSquared,
        int minimumChromaticDistanceSquared)
    {
        int neutralCb = cb - 128;
        int neutralCr = cr - 128;

        if (((neutralCb * neutralCb) + (neutralCr * neutralCr)) < minimumChromaticDistanceSquared)
        {
            return false;
        }

        int dCb = cb - centerCb;
        int dCr = cr - centerCr;

        return ((dCb * dCb) + (dCr * dCr)) <= radiusSquared;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int ScaleRadiusSquared(double radiusSquared, double scale)
    {
        return CeilingToInt(radiusSquared * scale * scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CeilingToInt(double value)
    {
        int truncated = (int)value;

        return value > truncated ? truncated + 1 : truncated;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundToInt(double value)
        => value >= 0
            ? (int)(value + 0.5)
            : (int)(value - 0.5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int NormalizeNonNegative(int value)
        => value < 0 ? 0 : value;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double NormalizeNonNegative(double value)
        => value < 0 ? 0 : value;

    private readonly record struct CrossSectionSegment(int X0, int Y0, int X1, int Y1);
}

public record ColoredPlayerMaskDetectionOptions(
    double RodColorModelRadiusScale = 0.5,
    double ExpansionColorModelRadiusScale = 1.5,
    int MaskMargin = 8,
    int AlongRodExpansionMargin = 5,
    int AlongRodAdaptiveExpansionStep = 2,
    int AlongRodAdaptiveExpansionEdgeDistance = 1,
    int AlongRodAdaptiveExpansionMaxExtra = 16,
    int AlongRodAllowedMisses = 3,
    int CrossRodAllowedEmptyScans = 2,
    int MinimumCrossSectionMatches = 2,
    int MaximumRectanglesPerRod = 128);
