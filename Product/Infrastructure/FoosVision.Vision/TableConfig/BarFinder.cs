// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableConfig.Processing;
using FoosVision.Vision.TableConfig.Processing.HoughLines;

namespace FoosVision.Vision.TableConfig;

internal class BarFinder
{
    private readonly record struct RoughBarCandidate(int Index, HoughLine Line, LineCoverageScore CoverageScore);

    private readonly record struct RoughBarSequence(int[] Indices, double Score);

    private readonly record struct FineBarCandidate(HoughLine Line, LineCoverageScore CoverageScore);

    private const int _HalfBarWidthTolerance = 40;
    private const double _HalfBarAngleTolerance = 3.0;
    private const int _RoughBarCoverageBinCount = 32;
    private const int _RoughBarCoverageHalfWidth = _HalfBarWidthTolerance / 2;
    private const int _RoughBarMinimumEdgePixelsPerBin = 2;
    private const double _RoughBarMinimumCoverage = 0.75;
    private const int _RoughBarSequenceCandidateLimit = 32;
    private const double _CenterLineMissingCoveragePenalty = 250.0;
    private const int _FineBarCoverageBinCount = _RoughBarCoverageBinCount;
    private const int _FineBarCoverageHalfWidth = 3;
    private const int _FineBarMinimumEdgePixelsPerBin = 2;
    private const double _FineBarMinimumCoverage = 0.75;
    private const double _GeometricBarWidthScale = 1.10;

    private static readonly Source _Log = new("Vision.BarFinder");

    private readonly int _Width;
    private readonly int _Height;
    private readonly Rectangle _FullImageRectangle;
    private readonly VerticalLineFinder _VerticalLineFinder;
    private readonly VerticalLineFinder _VerticalFineLineFinder;
    private readonly HoughLine[] _HoughLines;
    private readonly HoughLine[] _HoughLinesFine;
    private readonly PfPoint[] _PointsFinderPoints;
    private readonly List<RoughBarCandidateDiagnostic> _RoughBarCandidateDiagnostics = [];

    public BarFinder(int width, int height)
    {
        _Width = width;
        _Height = height;
        _FullImageRectangle = new(0, 0, width, height);
        _VerticalLineFinder = new VerticalLineFinder(width, height, -10, 10, 1.0);
        _VerticalFineLineFinder = new VerticalLineFinder(width, height, -10, 10, 0.1);
        _HoughLines = new HoughLine[LineFinder.MaxLineCount];
        _HoughLinesFine = new HoughLine[LineFinder.MaxLineCount];
        _PointsFinderPoints = new PfPoint[LineFinder.MaxLineCount];
    }

    public IReadOnlyList<RoughBarCandidateDiagnostic> LastRoughBarCandidates => _RoughBarCandidateDiagnostics;

    public IReadOnlyList<Bar> Find(byte[] y8CannyBuffer)
    {
        int count = _VerticalLineFinder.Find(y8CannyBuffer, _FullImageRectangle, 0.3, 0, 0, 30, _HoughLines);

        var lines = _HoughLines
            .Take(count)
            .Where(l => l.P0.X >= 0 && l.P0.X < _Width && l.P1.X >= 0 && l.P1.X <= _Width)
            .OrderBy(x => x.P0.X)
            .ToArray();

        var barTypes = Enum.GetValues<BarType>();
        var barCount = barTypes.Length;

        if (lines.Length < barCount)
        {
            _Log.Warning(
                "Find failed: not enough rough vertical lines. Required={RequiredLineCount} Actual={ActualLineCount}",
                barCount,
                lines.Length);
            return [];
        }

        var roughCandidates = GetRoughBarCandidates(y8CannyBuffer, lines);
        var selectableCandidates = roughCandidates
            .Where(candidate => candidate.CoverageScore.Coverage >= _RoughBarMinimumCoverage)
            .ToArray();

        if (selectableCandidates.Length < barCount)
        {
            _Log.Warning(
                "Find failed: not enough covered rough candidates. Required={RequiredLineCount} Actual={ActualLineCount} MinimumCoverage={MinimumCoverage}",
                barCount,
                selectableCandidates.Length,
                _RoughBarMinimumCoverage);
            UpdateRoughBarCandidateDiagnostics(roughCandidates, selectableCandidates, []);
            return [];
        }

        var barSequences = SelectBarCandidateSequences(y8CannyBuffer, selectableCandidates, barCount);

        if (barSequences.Count == 0)
        {
            _Log.Warning("Find failed: equally spaced bar candidates not found. Required={RequiredBarCount}", barCount);
            UpdateRoughBarCandidateDiagnostics(roughCandidates, selectableCandidates, []);
            return [];
        }

        List<Bar>? bestBars = null;
        int[] bestBarPointIndices = [];

        foreach (var sequence in barSequences)
        {
            if (!TryGetBars(y8CannyBuffer, barTypes, selectableCandidates, sequence.Indices, out List<Bar> bars))
            {
                continue;
            }

            var consistentBars = MakeBarsGeometricallyConsistent(bars);

            if (!IsPlausibleBarGeometry(consistentBars))
            {
                continue;
            }

            bestBars = consistentBars;
            bestBarPointIndices = sequence.Indices;
            break;
        }

        UpdateRoughBarCandidateDiagnostics(roughCandidates, selectableCandidates, bestBarPointIndices);

        if (bestBars is null)
        {
            _Log.Warning("Find failed: no rough bar sequence could be refined into plausible bars.");
            return [];
        }

        return bestBars;
    }

    private RoughBarCandidate[] GetRoughBarCandidates(byte[] y8CannyBuffer, HoughLine[] lines)
    {
        var candidates = new RoughBarCandidate[lines.Length];

        for (int i = 0; i < lines.Length; i++)
        {
            var coverageScore = LineCoverageScorer.ScoreVertical(
                y8CannyBuffer,
                _Width,
                _Height,
                _FullImageRectangle,
                lines[i],
                _RoughBarCoverageBinCount,
                _RoughBarCoverageHalfWidth,
                _RoughBarMinimumEdgePixelsPerBin);

            candidates[i] = new(i, lines[i], coverageScore);
        }

        return candidates;
    }

    private List<RoughBarSequence> SelectBarCandidateSequences(byte[] y8CannyBuffer, IReadOnlyList<RoughBarCandidate> candidates, int barCount)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            _PointsFinderPoints[i].X0 = candidates[i].Line.P0.X;
            _PointsFinderPoints[i].X1 = candidates[i].Line.P1.X;
        }

        return EquallySpacedPointsFinder
            .SelectSequences(_PointsFinderPoints, candidates.Count, barCount, _RoughBarSequenceCandidateLimit)
            .Select(sequence => new RoughBarSequence(
                sequence.Indices,
                sequence.Error + GetCenterLinePenalty(y8CannyBuffer, candidates, sequence.Indices)))
            .OrderBy(sequence => sequence.Score)
            .Take(_RoughBarSequenceCandidateLimit)
            .ToList();
    }

    private double GetCenterLinePenalty(byte[] y8CannyBuffer, IReadOnlyList<RoughBarCandidate> candidates, IReadOnlyList<int> sequence)
    {
        const int a5Index = 3;
        const int b5Index = 4;

        if (sequence.Count <= b5Index)
        {
            return 0.0;
        }

        var a5Line = candidates[sequence[a5Index]].Line;
        var b5Line = candidates[sequence[b5Index]].Line;
        HoughLine expectedCenterLine = new(
            new((a5Line.P0.X + b5Line.P0.X) / 2.0, 0),
            new((a5Line.P1.X + b5Line.P1.X) / 2.0, _Height),
            0,
            0.0,
            0,
            0);
        var coverageScore = LineCoverageScorer.ScoreVertical(
            y8CannyBuffer,
            _Width,
            _Height,
            _FullImageRectangle,
            expectedCenterLine,
            _RoughBarCoverageBinCount,
            _RoughBarCoverageHalfWidth,
            _RoughBarMinimumEdgePixelsPerBin);

        return (1.0 - coverageScore.Coverage) * _CenterLineMissingCoveragePenalty;
    }

    private void UpdateRoughBarCandidateDiagnostics(
        IReadOnlyList<RoughBarCandidate> roughCandidates,
        IReadOnlyList<RoughBarCandidate> selectableCandidates,
        IReadOnlyCollection<int> selectedIndices)
    {
        _RoughBarCandidateDiagnostics.Clear();
        var selectedOriginalIndices = selectedIndices
            .Select(index => selectableCandidates[index].Index)
            .ToHashSet();

        foreach (var candidate in roughCandidates)
        {
            _RoughBarCandidateDiagnostics.Add(new(
                candidate.Index,
                candidate.Line,
                candidate.CoverageScore,
                selectedOriginalIndices.Contains(candidate.Index)));
        }
    }

    private bool TryGetBars(
        byte[] y8CannyBuffer,
        IReadOnlyList<BarType> barTypes,
        IReadOnlyList<RoughBarCandidate> selectableCandidates,
        IReadOnlyList<int> barPointsIndices,
        out List<Bar> bars)
    {
        bars = [];

        for (int i = 0; i < barTypes.Count; i++)
        {
            var roughCandidate = selectableCandidates[barPointsIndices[i]];
            Bar? bar = GetBarFineEstimation(y8CannyBuffer, barTypes[i], roughCandidate.Line);

            if (bar is null)
            {
                return false;
            }

            bars.Add(bar);
        }

        return true;
    }

    private Bar? GetBarFineEstimation(byte[] y8CannyBuffer, BarType type, HoughLine line)
    {
        int rawX0 = (int)Math.Floor(Math.Min(line.P0.X, line.P1.X)) - _HalfBarWidthTolerance;
        int rawY0 = (int)Math.Floor(Math.Min(line.P0.Y, line.P1.Y));
        int rawX1 = (int)Math.Ceiling(Math.Max(line.P0.X, line.P1.X)) + _HalfBarWidthTolerance;
        int rawY1 = (int)Math.Ceiling(Math.Max(line.P0.Y, line.P1.Y));

        if (!FieldDetectorMath.TryClampRectangle(_Width, _Height, rawX0, rawY0, rawX1 - rawX0, rawY1 - rawY0, out Rectangle lineRect))
        {
            _Log.Warning(
                "GetBarFineEstimation skipped: invalid fine rect. BarType={BarType} Raw=({X},{Y},{Width},{Height}) LineP0=({P0X},{P0Y}) LineP1=({P1X},{P1Y})",
                type,
                rawX0,
                rawY0,
                rawX1 - rawX0,
                rawY1 - rawY0,
                line.P0.X,
                line.P0.Y,
                line.P1.X,
                line.P1.Y);
            return null;
        }

        var fineLineCount = _VerticalFineLineFinder.Find(
            y8CannyBuffer, lineRect, 0.5, 0, 0, 0, _HoughLinesFine,
            line.R - _HalfBarWidthTolerance, line.R + _HalfBarWidthTolerance,
            line.Angle - _HalfBarAngleTolerance, line.Angle + _HalfBarAngleTolerance);

        if (fineLineCount >= LineFinder.MaxLineCount)
        {
            _Log.Warning("GetBarFineEstimation failed: fine line result hit the buffer limit. BarType={BarType} Limit={Limit}", type, LineFinder.MaxLineCount);
            return null;
        }

        var coveredFineCandidates = GetFineBarCandidates(y8CannyBuffer, fineLineCount, lineRect)
            .Where(candidate => candidate.CoverageScore.Coverage >= _FineBarMinimumCoverage)
            .OrderBy(candidate => candidate.Line.Angle)
            .ToList();

        int skipCount = coveredFineCandidates.Count / 4;
        var middleHalf = coveredFineCandidates
            .Skip(skipCount)
            .Take(coveredFineCandidates.Count - (skipCount * 2))
            .OrderBy(candidate => candidate.Line.P0.X)
            .ToArray();

        int middleHalfCount = middleHalf.Length;
        if (middleHalfCount < 2)
        {
            _Log.Warning(
                "GetBarFineEstimation failed: not enough covered middle-half fine lines. BarType={BarType} FineLineCount={FineLineCount} CoveredFineLineCount={CoveredFineLineCount} Count={Count} MinimumCoverage={MinimumCoverage}",
                type,
                fineLineCount,
                coveredFineCandidates.Count,
                middleHalfCount,
                _FineBarMinimumCoverage);
            return null;
        }

        double x0Min = middleHalf.Min(candidate => candidate.Line.P0.X);
        double x0Max = middleHalf.Max(candidate => candidate.Line.P0.X);
        double x1Min = middleHalf.Min(candidate => candidate.Line.P1.X);
        double x1Max = middleHalf.Max(candidate => candidate.Line.P1.X);

        Line left = new(new(x0Min, 0), new(x1Min, _Height));
        Line middle = new(new((x0Min + x0Max) / 2.0, 0), new((x1Min + x1Max) / 2.0, _Height));
        Line right = new(new(x0Max, 0), new(x1Max, _Height));
        Bar bar = new(type, left, middle, right);

        return bar;
    }

    private FineBarCandidate[] GetFineBarCandidates(byte[] y8CannyBuffer, int count, Rectangle rect)
    {
        var candidates = new FineBarCandidate[count];

        for (int i = 0; i < count; i++)
        {
            var coverageScore = LineCoverageScorer.ScoreVertical(
                y8CannyBuffer,
                _Width,
                _Height,
                rect,
                _HoughLinesFine[i],
                _FineBarCoverageBinCount,
                _FineBarCoverageHalfWidth,
                _FineBarMinimumEdgePixelsPerBin);

            candidates[i] = new(_HoughLinesFine[i], coverageScore);
        }

        return candidates;
    }

    private List<Bar> MakeBarsGeometricallyConsistent(List<Bar> bars)
    {
        double width0 = GetMedianWidth(bars, static bar => bar.Right.P0.X - bar.Left.P0.X) * _GeometricBarWidthScale;
        double width1 = GetMedianWidth(bars, static bar => bar.Right.P1.X - bar.Left.P1.X) * _GeometricBarWidthScale;

        List<Bar> consistentBars = [];

        foreach (var bar in bars)
        {
            double centerX0 = bar.Center.P0.X;
            double centerX1 = bar.Center.P1.X;
            Line left = new(new(centerX0 - (width0 / 2.0), 0), new(centerX1 - (width1 / 2.0), _Height));
            Line right = new(new(centerX0 + (width0 / 2.0), 0), new(centerX1 + (width1 / 2.0), _Height));

            consistentBars.Add(new(bar.Type, left, bar.Center, right));
        }

        return consistentBars;
    }

    private static double GetMedianWidth(IReadOnlyList<Bar> bars, Func<Bar, double> widthSelector)
    {
        var widths = bars
            .Select(widthSelector)
            .Select(Math.Abs)
            .Order()
            .ToArray();

        int middle = widths.Length / 2;

        if (widths.Length % 2 == 1)
        {
            return widths[middle];
        }

        return (widths[middle - 1] + widths[middle]) / 2.0;
    }

    private bool IsPlausibleBarGeometry(IReadOnlyList<Bar> bars)
    {
        double[] centerX = [.. bars.Select(bar => FieldDetectorMath.GetLineMidX(bar.Center))];

        if (centerX.Any(static x => !double.IsFinite(x)))
        {
            _Log.Warning("Bar geometry rejected: non-finite bar center coordinate.");
            return false;
        }

        for (int i = 1; i < centerX.Length; i++)
        {
            if (centerX[i] <= centerX[i - 1])
            {
                _Log.Warning(
                    "Bar geometry rejected: bar centers are not strictly increasing. Index={Index} Previous={PreviousX} Current={CurrentX}",
                    i,
                    centerX[i - 1],
                    centerX[i]);
                return false;
            }
        }

        double spread = centerX[^1] - centerX[0];
        double minimumSpread = _Width * 0.45;
        if (spread < minimumSpread)
        {
            _Log.Warning(
                "Bar geometry rejected: bar centers do not span enough of the image. Spread={Spread} Minimum={MinimumSpread} First={FirstX} Last={LastX}",
                spread,
                minimumSpread,
                centerX[0],
                centerX[^1]);
            return false;
        }

        return true;
    }
}
