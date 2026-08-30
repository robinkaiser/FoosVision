// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.Common;
using FoosVision.Vision.TableConfig.Processing.HoughLines;

namespace FoosVision.Vision.TableConfig;

/// <summary>
/// Finds table-light occlusions in two stages. The strict stage is the normal path: it uses stronger Hough evidence
/// and fine refinement to preserve the most accurate edge geometry. The relaxed stage runs only if the strict stage
/// finds nothing; it admits weaker middle-region lines, but compensates with pair-angle, boundary-plausibility, and
/// parallel-band checks so partially weak light edges can still be recovered without changing the normal cases.
/// </summary>
internal class LightOcclusionFinder
{
    private readonly record struct LightCandidate(HoughLine Line, LineCoverageScore CoverageScore, double MidY);
    private readonly record struct SearchStage(
        string Name,
        double AccumulatorThresholdRatio,
        double MinimumAccumulatorRatio,
        bool UseRefinement,
        bool NormalizeLinePair,
        bool RequirePairAngleConsistency,
        bool RequireRoughBoundaryPlausibility);

    private const double _StrictHorizontalLineAccumulatorThresholdRatio = 0.3;
    private const double _StrictHorizontalLineMinimumAccumulatorRatio = 0.02;
    private const double _RelaxedHorizontalLineAccumulatorThresholdRatio = 0.1;
    private const double _RelaxedHorizontalLineMinimumAccumulatorRatio = 0.01;
    private const int _LightSearchBoundaryInset = 10;
    private const int _LightCoverageBinCount = 32;
    private const double _LightStrongMinimumCoverage = 0.56;
    private const double _LightStrongMinimumLongestRunCoverage = 0.06;
    private const double _LightWeakMinimumCoverage = 0.25;
    private const double _LightWeakMinimumLongestRunCoverage = 0.06;
    private const double _LightMinimumHeight = 30.0;
    private const double _LightMaximumHeight = 90.0;
    private const double _LightAngleTolerance = 7.0;
    private const double _LightPairAngleTolerance = 3.0;
    private const double _LightAccumulatorScoreWeight = 0.00005;
    private const int _LightColorContrastSampleStep = 2;
    private const int _LightColorContrastSampleOffset = 10;
    private const int _LightMinimumColorDistance = 35;
    private const int _LightMinimumColorDistanceSquared = _LightMinimumColorDistance * _LightMinimumColorDistance;
    private const int _LightMinimumColorContrastingSamplesPerBin = 8;
    private const int _LightBandContrastSampleStep = 2;
    private const int _LightMinimumBandContrastingSamplesPerBin = 4;
    private const double _LightPairMinimumBandCoverage = 0.5;

    private static readonly Source _Log = new("Vision.LightOcclusionFinder");
    private static readonly SearchStage _StrictStage = new(
        "strict",
        _StrictHorizontalLineAccumulatorThresholdRatio,
        _StrictHorizontalLineMinimumAccumulatorRatio,
        UseRefinement: true,
        NormalizeLinePair: false,
        RequirePairAngleConsistency: false,
        RequireRoughBoundaryPlausibility: false);

    private static readonly SearchStage _RelaxedStage = new(
        "relaxed",
        _RelaxedHorizontalLineAccumulatorThresholdRatio,
        _RelaxedHorizontalLineMinimumAccumulatorRatio,
        UseRefinement: false,
        NormalizeLinePair: true,
        RequirePairAngleConsistency: true,
        RequireRoughBoundaryPlausibility: true);

    private readonly int _Width;
    private readonly int _Height;
    private readonly HorizontalLineFinder _HorizontalLineFinder;
    private readonly HorizontalLineRefiner _HorizontalLineRefiner;
    private readonly HoughLine[] _HoughLines;

    public LightOcclusionFinder(int width, int height)
    {
        _Width = width;
        _Height = height;
        _HorizontalLineFinder = new HorizontalLineFinder(width, height, 80, 100, 1.0);
        _HorizontalLineRefiner = new HorizontalLineRefiner(width, height);
        _HoughLines = new HoughLine[LineFinder.MaxLineCount];
    }

    public bool TryGetSearchRectangle(Trapezium boundary, TableBars bars, out Rectangle rect)
    {
        double x0 = Math.Max(bars.B3.Right.P0.X, bars.B3.Right.P1.X);
        double x1 = Math.Min(bars.A3.Left.P0.X, bars.A3.Left.P1.X);
        double y0 = Math.Max(boundary.UpperLeft.Y, boundary.UpperRight.Y) + _LightSearchBoundaryInset;
        double y1 = Math.Min(boundary.LowerLeft.Y, boundary.LowerRight.Y) - _LightSearchBoundaryInset;

        int rawX0 = (int)Math.Floor(x0);
        int rawY0 = (int)Math.Floor(y0);
        int rawWidth = (int)Math.Ceiling(x1) - rawX0;
        int rawHeight = (int)Math.Ceiling(y1) - rawY0;

        return FieldDetectorMath.TryClampRectangle(_Width, _Height, rawX0, rawY0, rawWidth, rawHeight, out rect);
    }

    public IReadOnlyList<Trapezium> Find(byte[] frameBufferRGBA8888, byte[] y8CannyBuffer, Trapezium boundary, Rectangle searchRect)
    {
        if (TryFind(
            _StrictStage,
            frameBufferRGBA8888,
            y8CannyBuffer,
            boundary,
            searchRect,
            out Trapezium occlusion))
        {
            return [occlusion];
        }

        if (TryFind(
            _RelaxedStage,
            frameBufferRGBA8888,
            y8CannyBuffer,
            boundary,
            searchRect,
            out occlusion))
        {
            return [occlusion];
        }

        return [];
    }

    private bool TryFind(
        SearchStage stage,
        byte[] frameBufferRGBA8888,
        byte[] y8CannyBuffer,
        Trapezium boundary,
        Rectangle searchRect,
        out Trapezium occlusion)
    {
        occlusion = default;

        var lineCount = _HorizontalLineFinder.Find(y8CannyBuffer, searchRect, stage.AccumulatorThresholdRatio, 0, 0, 10, _HoughLines);
        if (lineCount >= LineFinder.MaxLineCount)
        {
            _Log.Warning(
                "Find {StageName} stage failed: rough line result hit the buffer limit. Limit={Limit}",
                stage.Name,
                LineFinder.MaxLineCount);
            return false;
        }

        lineCount = FieldDetectorMath.KeepStrongAccumulatorLines(_HoughLines, lineCount, stage.MinimumAccumulatorRatio);

        LightCandidate[] candidates = new LightCandidate[lineCount];
        int candidateCount = 0;

        for (int i = 0; i < lineCount; i++)
        {
            var line = _HoughLines[i];

            if (Math.Abs(line.Angle - 90.0) > _LightAngleTolerance)
            {
                continue;
            }

            var coverageScore = ScoreLightColorContrast(
                frameBufferRGBA8888,
                _Width,
                _Height,
                searchRect,
                line,
                _LightCoverageBinCount);

            if (coverageScore.Coverage < _LightWeakMinimumCoverage ||
                coverageScore.LongestRunCoverage < _LightWeakMinimumLongestRunCoverage)
            {
                continue;
            }

            candidates[candidateCount] = new(line, coverageScore, FieldDetectorMath.GetLineMidY(line));
            candidateCount++;
        }

        if (!TrySelectLightLinePair(
            candidates,
            candidateCount,
            frameBufferRGBA8888,
            _Width,
            _Height,
            boundary,
            searchRect,
            stage.RequirePairAngleConsistency,
            stage.RequireRoughBoundaryPlausibility,
            out HoughLine upperLine,
            out HoughLine lowerLine))
        {
            return false;
        }

        if (stage.UseRefinement)
        {
            HoughLine? refinedUpperLine = _HorizontalLineRefiner.Refine(y8CannyBuffer, upperLine, "FindLightOcclusions", searchRect);
            HoughLine? refinedLowerLine = _HorizontalLineRefiner.Refine(y8CannyBuffer, lowerLine, "FindLightOcclusions", searchRect);

            if (!refinedUpperLine.HasValue || !refinedLowerLine.HasValue)
            {
                return false;
            }

            upperLine = refinedUpperLine.Value;
            lowerLine = refinedLowerLine.Value;
        }
        else if (stage.NormalizeLinePair)
        {
            NormalizeLightLinePair(
                _Width,
                searchRect,
                upperLine,
                lowerLine,
                out HoughLine normalizedUpperLine,
                out HoughLine normalizedLowerLine);

            upperLine = normalizedUpperLine;
            lowerLine = normalizedLowerLine;
        }

        if (!TryCreateOcclusion(boundary, upperLine, lowerLine, out occlusion))
        {
            return false;
        }

        return true;
    }

    private static bool TrySelectLightLinePair(
        LightCandidate[] candidates,
        int candidateCount,
        byte[] frameBufferRGBA8888,
        int imageWidth,
        int imageHeight,
        Trapezium boundary,
        Rectangle searchRect,
        bool requirePairAngleConsistency,
        bool requireRoughBoundaryPlausibility,
        out HoughLine upperLine,
        out HoughLine lowerLine)
    {
        upperLine = default;
        lowerLine = default;

        double bestScore = double.MinValue;

        for (int upperIndex = 0; upperIndex < candidateCount; upperIndex++)
        {
            var upperCandidate = candidates[upperIndex];

            if (upperCandidate.CoverageScore.Coverage < _LightStrongMinimumCoverage ||
                upperCandidate.CoverageScore.LongestRunCoverage < _LightStrongMinimumLongestRunCoverage)
            {
                continue;
            }

            for (int lowerIndex = 0; lowerIndex < candidateCount; lowerIndex++)
            {
                var lowerCandidate = candidates[lowerIndex];
                double height = lowerCandidate.MidY - upperCandidate.MidY;

                if (height < _LightMinimumHeight || height > _LightMaximumHeight)
                {
                    continue;
                }

                if (requirePairAngleConsistency &&
                    Math.Abs(upperCandidate.Line.Angle - lowerCandidate.Line.Angle) > _LightPairAngleTolerance)
                {
                    continue;
                }

                var bandContrastScore = ScoreLightBandContrast(
                    frameBufferRGBA8888,
                    imageWidth,
                    imageHeight,
                    searchRect,
                    upperCandidate.Line,
                    lowerCandidate.Line,
                    _LightCoverageBinCount);

                if (bandContrastScore.Coverage < _LightPairMinimumBandCoverage)
                {
                    continue;
                }

                if (requireRoughBoundaryPlausibility &&
                    !TryCreateOcclusion(boundary, upperCandidate.Line, lowerCandidate.Line, out _))
                {
                    continue;
                }

                double score =
                    (2.0 * upperCandidate.CoverageScore.Coverage) +
                    (0.5 * upperCandidate.CoverageScore.LongestRunCoverage) +
                    (2.0 * lowerCandidate.CoverageScore.Coverage) +
                    (0.5 * lowerCandidate.CoverageScore.LongestRunCoverage) +
                    (2.0 * bandContrastScore.Coverage) +
                    ((upperCandidate.Line.Accumulator + lowerCandidate.Line.Accumulator) * _LightAccumulatorScoreWeight);

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                upperLine = upperCandidate.Line;
                lowerLine = lowerCandidate.Line;
            }
        }

        return bestScore > double.MinValue;
    }

    private static LineCoverageScore ScoreLightBandContrast(
        byte[] frameBufferRGBA8888,
        int imageWidth,
        int imageHeight,
        Rectangle rect,
        HoughLine upperLine,
        HoughLine lowerLine,
        int binCount)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || binCount <= 0)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        Rectangle imageRect = new(0, 0, imageWidth, imageHeight);
        Rectangle scanRect = Rectangle.Intersect(rect, imageRect);

        if (scanRect.IsEmpty)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        double upperDx = upperLine.P1.X - upperLine.P0.X;
        double upperDy = upperLine.P1.Y - upperLine.P0.Y;
        double lowerDx = lowerLine.P1.X - lowerLine.P0.X;
        double lowerDy = lowerLine.P1.Y - lowerLine.P0.Y;

        if (upperDx == 0.0 || lowerDx == 0.0)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        int effectiveBinCount = Math.Min(binCount, scanRect.Width);
        int supportedBins = 0;
        int currentRun = 0;
        int longestRun = 0;
        int contrastingSampleCount = 0;

        for (int bin = 0; bin < effectiveBinCount; bin++)
        {
            int x0 = scanRect.X + (bin * scanRect.Width / effectiveBinCount);
            int x1 = scanRect.X + ((bin + 1) * scanRect.Width / effectiveBinCount);
            int binContrastingSampleCount = 0;

            for (int x = x0; x < x1; x += _LightBandContrastSampleStep)
            {
                double upperY = upperLine.P0.Y + ((x - upperLine.P0.X) * upperDy / upperDx);
                double lowerY = lowerLine.P0.Y + ((x - lowerLine.P0.X) * lowerDy / lowerDx);
                double insideY = (upperY + lowerY) / 2.0;

                if (!double.IsFinite(upperY) || !double.IsFinite(lowerY) || !double.IsFinite(insideY))
                {
                    continue;
                }

                int yInside = FieldDetectorMath.RoundToNearestInt(insideY);
                int yAbove = FieldDetectorMath.RoundToNearestInt(upperY - _LightColorContrastSampleOffset);
                int yBelow = FieldDetectorMath.RoundToNearestInt(lowerY + _LightColorContrastSampleOffset);

                if (x < 0 || x >= imageWidth ||
                    yInside < 0 || yInside >= imageHeight ||
                    yAbove < 0 || yAbove >= imageHeight ||
                    yBelow < 0 || yBelow >= imageHeight)
                {
                    continue;
                }

                int insideIndex = ((yInside * imageWidth) + x) * 4;
                int aboveIndex = ((yAbove * imageWidth) + x) * 4;
                int belowIndex = ((yBelow * imageWidth) + x) * 4;

                int distanceToAbove = GetColorDistanceSquared(frameBufferRGBA8888, insideIndex, aboveIndex);
                int distanceToBelow = GetColorDistanceSquared(frameBufferRGBA8888, insideIndex, belowIndex);

                if (Math.Min(distanceToAbove, distanceToBelow) < _LightMinimumColorDistanceSquared)
                {
                    continue;
                }

                binContrastingSampleCount++;
            }

            contrastingSampleCount += binContrastingSampleCount;

            if (binContrastingSampleCount < _LightMinimumBandContrastingSamplesPerBin)
            {
                currentRun = 0;
                continue;
            }

            supportedBins++;
            currentRun++;
            longestRun = Math.Max(longestRun, currentRun);
        }

        return new LineCoverageScore(supportedBins, effectiveBinCount, longestRun, contrastingSampleCount);
    }

    private static LineCoverageScore ScoreLightColorContrast(
        byte[] frameBufferRGBA8888,
        int imageWidth,
        int imageHeight,
        Rectangle rect,
        HoughLine line,
        int binCount)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || binCount <= 0)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        Rectangle imageRect = new(0, 0, imageWidth, imageHeight);
        Rectangle scanRect = Rectangle.Intersect(rect, imageRect);

        if (scanRect.IsEmpty)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        double dx = line.P1.X - line.P0.X;
        double dy = line.P1.Y - line.P0.Y;

        if (dx == 0.0)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        double length = Math.Sqrt((dx * dx) + (dy * dy));

        if (length <= 0.0)
        {
            return new LineCoverageScore(0, 0, 0, 0);
        }

        double normalX = -dy / length;
        double normalY = dx / length;
        int effectiveBinCount = Math.Min(binCount, scanRect.Width);
        int supportedBins = 0;
        int currentRun = 0;
        int longestRun = 0;
        int contrastingSampleCount = 0;

        for (int bin = 0; bin < effectiveBinCount; bin++)
        {
            int x0 = scanRect.X + (bin * scanRect.Width / effectiveBinCount);
            int x1 = scanRect.X + ((bin + 1) * scanRect.Width / effectiveBinCount);
            int binContrastingSampleCount = 0;

            for (int x = x0; x < x1; x += _LightColorContrastSampleStep)
            {
                double lineY = line.P0.Y + ((x - line.P0.X) * dy / dx);

                if (!double.IsFinite(lineY))
                {
                    continue;
                }

                int xA = FieldDetectorMath.RoundToNearestInt(x - (normalX * _LightColorContrastSampleOffset));
                int yA = FieldDetectorMath.RoundToNearestInt(lineY - (normalY * _LightColorContrastSampleOffset));
                int xB = FieldDetectorMath.RoundToNearestInt(x + (normalX * _LightColorContrastSampleOffset));
                int yB = FieldDetectorMath.RoundToNearestInt(lineY + (normalY * _LightColorContrastSampleOffset));

                if (xA < 0 || xA >= imageWidth ||
                    xB < 0 || xB >= imageWidth ||
                    yA < 0 || yA >= imageHeight ||
                    yB < 0 || yB >= imageHeight)
                {
                    continue;
                }

                int indexA = ((yA * imageWidth) + xA) * 4;
                int indexB = ((yB * imageWidth) + xB) * 4;
                int dr = frameBufferRGBA8888[indexA] - frameBufferRGBA8888[indexB];
                int dg = frameBufferRGBA8888[indexA + 1] - frameBufferRGBA8888[indexB + 1];
                int db = frameBufferRGBA8888[indexA + 2] - frameBufferRGBA8888[indexB + 2];
                int distanceSquared = (dr * dr) + (dg * dg) + (db * db);

                if (distanceSquared < _LightMinimumColorDistanceSquared)
                {
                    continue;
                }

                binContrastingSampleCount++;
            }

            contrastingSampleCount += binContrastingSampleCount;

            if (binContrastingSampleCount < _LightMinimumColorContrastingSamplesPerBin)
            {
                currentRun = 0;
                continue;
            }

            supportedBins++;
            currentRun++;
            longestRun = Math.Max(longestRun, currentRun);
        }

        return new LineCoverageScore(supportedBins, effectiveBinCount, longestRun, contrastingSampleCount);
    }

    private static void NormalizeLightLinePair(
        int imageWidth,
        Rectangle searchRect,
        HoughLine upperLine,
        HoughLine lowerLine,
        out HoughLine normalizedUpperLine,
        out HoughLine normalizedLowerLine)
    {
        double upperWeight = Math.Max(upperLine.Accumulator, 1);
        double lowerWeight = Math.Max(lowerLine.Accumulator, 1);
        double angle = ((upperLine.Angle * upperWeight) + (lowerLine.Angle * lowerWeight)) / (upperWeight + lowerWeight);
        double radians = angle * Math.PI / 180.0;
        double sin = Math.Sin(radians);

        if (sin == 0.0)
        {
            normalizedUpperLine = upperLine;
            normalizedLowerLine = lowerLine;
            return;
        }

        double slope = -Math.Cos(radians) / sin;
        double anchorX = searchRect.X + (searchRect.Width / 2.0);
        double upperAnchorY = GetLineYAtX(upperLine, anchorX);
        double lowerAnchorY = GetLineYAtX(lowerLine, anchorX);

        normalizedUpperLine = CreateLineThrough(anchorX, upperAnchorY, slope, imageWidth, angle, upperLine);
        normalizedLowerLine = CreateLineThrough(anchorX, lowerAnchorY, slope, imageWidth, angle, lowerLine);
    }

    private static HoughLine CreateLineThrough(double anchorX, double anchorY, double slope, int imageWidth, double angle, HoughLine source)
    {
        double y0 = anchorY - (anchorX * slope);
        double y1 = anchorY + ((imageWidth - anchorX) * slope);

        return new(new Point(0.0, y0), new Point(imageWidth, y1), source.R, angle, source.Theta, source.Accumulator);
    }

    private static double GetLineYAtX(HoughLine line, double x)
    {
        double dx = line.P1.X - line.P0.X;

        if (dx == 0.0)
        {
            return FieldDetectorMath.GetLineMidY(line);
        }

        double dy = line.P1.Y - line.P0.Y;
        return line.P0.Y + ((x - line.P0.X) * dy / dx);
    }

    private static int GetColorDistanceSquared(byte[] frameBufferRGBA8888, int indexA, int indexB)
    {
        int dr = frameBufferRGBA8888[indexA] - frameBufferRGBA8888[indexB];
        int dg = frameBufferRGBA8888[indexA + 1] - frameBufferRGBA8888[indexB + 1];
        int db = frameBufferRGBA8888[indexA + 2] - frameBufferRGBA8888[indexB + 2];

        return (dr * dr) + (dg * dg) + (db * db);
    }

    private static bool TryCreateOcclusion(
        Trapezium boundary,
        HoughLine upperHoughLine,
        HoughLine lowerHoughLine,
        out Trapezium occlusion)
    {
        Line leftBoundary = new(boundary.UpperLeft, boundary.LowerLeft);
        Line rightBoundary = new(boundary.UpperRight, boundary.LowerRight);
        Line upperLine = new(upperHoughLine.P0, upperHoughLine.P1);
        Line lowerLine = new(lowerHoughLine.P0, lowerHoughLine.P1);

        bool hasUpperLeft = Geometry.TryIntersect(leftBoundary, upperLine, out Point upperLeft);
        bool hasUpperRight = Geometry.TryIntersect(rightBoundary, upperLine, out Point upperRight);
        bool hasLowerLeft = Geometry.TryIntersect(leftBoundary, lowerLine, out Point lowerLeft);
        bool hasLowerRight = Geometry.TryIntersect(rightBoundary, lowerLine, out Point lowerRight);

        if (!hasUpperLeft || !hasUpperRight || !hasLowerLeft || !hasLowerRight)
        {
            occlusion = default;
            return false;
        }

        double leftHeight = lowerLeft.Y - upperLeft.Y;
        double rightHeight = lowerRight.Y - upperRight.Y;

        if (!IsHeightPlausible(leftHeight) || !IsHeightPlausible(rightHeight))
        {
            occlusion = default;
            return false;
        }

        occlusion = new(upperLeft, upperRight, lowerLeft, lowerRight);
        return true;
    }

    private static bool IsHeightPlausible(double height)
    {
        return height >= _LightMinimumHeight && height <= _LightMaximumHeight;
    }
}
