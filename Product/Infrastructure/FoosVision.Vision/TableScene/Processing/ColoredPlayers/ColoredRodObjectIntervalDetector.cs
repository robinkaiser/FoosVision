// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public class ColoredRodObjectIntervalDetector
{
    private readonly ColoredRodObjectIntervalDetectionOptions _Options;
    private readonly RodColorSampler _RodColorSampler;
    private readonly RodEdgePeakIntervalFinder _RodEdgePeakIntervalFinder;
    private readonly RodColorEdgeChangeScorer _RodColorEdgeChangeScorer;
    private readonly bool[] _OccludedSamples;
    private readonly RodColoredObjectIntervals[] _Rods;
    private readonly RodObjectInterval[][] _Intervals;
    private readonly ReadOnlyBuffer<RodObjectInterval>[] _IntervalLists;
    private readonly int[][] _SampleX;
    private readonly int[][] _SampleY;
    private readonly bool[][] _SampleOccluded;
    private readonly ColorFeature[][] _SampleFeatures;
    private readonly double[][] _EdgeScores;

    public ColoredRodObjectIntervalDetector(int width, int height, ColoredRodObjectIntervalDetectionOptions? options = null)
    {
        _Options = options ?? new ColoredRodObjectIntervalDetectionOptions();

        int capacity = Math.Max(width, height);
        _RodColorSampler = new(width, height);
        _RodEdgePeakIntervalFinder = new(capacity);
        _RodColorEdgeChangeScorer = new(capacity);
        _OccludedSamples = new bool[capacity];
        _Rods = new RodColoredObjectIntervals[8];
        _Intervals = new RodObjectInterval[8][];
        _IntervalLists = new ReadOnlyBuffer<RodObjectInterval>[8];
        _SampleX = new int[8][];
        _SampleY = new int[8][];
        _SampleOccluded = new bool[8][];
        _SampleFeatures = new ColorFeature[8][];
        _EdgeScores = new double[8][];

        for (int i = 0; i < 8; i++)
        {
            _Intervals[i] = new RodObjectInterval[capacity];
            _IntervalLists[i] = new(_Intervals[i]);
            _SampleX[i] = new int[capacity];
            _SampleY[i] = new int[capacity];
            _SampleOccluded[i] = new bool[capacity];
            _SampleFeatures[i] = new ColorFeature[capacity];
            _EdgeScores[i] = new double[capacity];
        }
    }

    public ColoredRodObjectIntervalDetection Detect(byte[] frameBufferRGBA8888, PlayingField field)
    {
        int count = 0;

        DetectFieldBar(frameBufferRGBA8888, field.Bars.A1, field.Occlusions, -1, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.A2, field.Occlusions, -1, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.B3, field.Occlusions, -1, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.A5, field.Occlusions, -1, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.B5, field.Occlusions, -1, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.A3, field.Occlusions, -1, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.B2, field.Occlusions, -1, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.B1, field.Occlusions, -1, ref count);

        double edgeMinimumScore = CalculateGlobalMinimumScore(_Rods, count);
        count = 0;

        DetectFieldBar(frameBufferRGBA8888, field.Bars.A1, field.Occlusions, edgeMinimumScore, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.A2, field.Occlusions, edgeMinimumScore, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.B3, field.Occlusions, edgeMinimumScore, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.A5, field.Occlusions, edgeMinimumScore, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.B5, field.Occlusions, edgeMinimumScore, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.A3, field.Occlusions, edgeMinimumScore, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.B2, field.Occlusions, edgeMinimumScore, ref count);
        DetectFieldBar(frameBufferRGBA8888, field.Bars.B1, field.Occlusions, edgeMinimumScore, ref count);

        return new ColoredRodObjectIntervalDetection(_Rods);
    }

    public RodColoredObjectIntervals DetectBar(byte[] frameBufferRGBA8888, Bar bar)
        => DetectBar(frameBufferRGBA8888, bar, Array.Empty<Trapezium>(), -1, 0);

    private RodColoredObjectIntervals DetectBar(
        byte[] frameBufferRGBA8888,
        Bar bar,
        IReadOnlyList<Trapezium> occlusions,
        double edgeMinimumScore,
        int bufferIndex)
    {
        RodColorSampleProfile profile = _RodColorSampler.Sample(frameBufferRGBA8888, bar);

        if (profile.Count == 0)
        {
            _IntervalLists[bufferIndex].SetCount(0);
            return new RodColoredObjectIntervals(
                bar.Type,
                _IntervalLists[bufferIndex],
                new(_SampleX[bufferIndex], _SampleY[bufferIndex], _SampleOccluded[bufferIndex], _SampleFeatures[bufferIndex], 0),
                new(_EdgeScores[bufferIndex], 0, 0));
        }

        MarkOccludedSamples(profile, occlusions);

        RodColorEdgeScoreScan edgeScoreScan = _RodColorEdgeChangeScorer.Calculate(
            profile.Features,
            profile.Count,
            _OccludedSamples,
            _Options.EdgeWindowLength);
        double effectiveEdgeMinimumScore = edgeMinimumScore >= 0
            ? edgeMinimumScore
            : CalculateMinimumScore(edgeScoreScan.Scores, _OccludedSamples, edgeScoreScan.Count);
        RodObjectIntervalScan intervalScan = _RodEdgePeakIntervalFinder.Find(
            edgeScoreScan.Scores,
            edgeScoreScan.Count,
            effectiveEdgeMinimumScore,
            _Options.EdgePeakNeighborhood,
            _Options.EdgePairMaximumDistance);

        int[] sampleX = _SampleX[bufferIndex];
        int[] sampleY = _SampleY[bufferIndex];
        bool[] sampleOccluded = _SampleOccluded[bufferIndex];
        ColorFeature[] sampleFeatures = _SampleFeatures[bufferIndex];
        double[] edgeScores = _EdgeScores[bufferIndex];
        RodObjectInterval[] intervals = _Intervals[bufferIndex];

        for (int i = 0; i < profile.Count; i++)
        {
            sampleX[i] = profile.Centers[i].X;
            sampleY[i] = profile.Centers[i].Y;
            sampleOccluded[i] = _OccludedSamples[i];
            sampleFeatures[i] = profile.Features[i];
        }

        Array.Copy(edgeScoreScan.Scores, edgeScores, edgeScoreScan.Count);

        for (int i = 0; i < intervalScan.Count; i++)
        {
            intervals[i] = intervalScan.Intervals[i];
        }

        _IntervalLists[bufferIndex].SetCount(intervalScan.Count);

        return new RodColoredObjectIntervals(
            bar.Type,
            _IntervalLists[bufferIndex],
            new(sampleX, sampleY, sampleOccluded, sampleFeatures, profile.Count),
            new(edgeScores, effectiveEdgeMinimumScore, edgeScoreScan.Count));
    }

    private void DetectFieldBar(
        byte[] frameBufferRGBA8888,
        Bar bar,
        IReadOnlyList<Trapezium> occlusions,
        double edgeMinimumScore,
        ref int count)
    {
        _Rods[count] = DetectBar(frameBufferRGBA8888, bar, occlusions, edgeMinimumScore, count);
        count++;
    }

    private double CalculateGlobalMinimumScore(RodColoredObjectIntervals[] rods, int count)
    {
        double max = 0;

        for (int i = 0; i < count; i++)
        {
            RodColoredObjectIntervals rod = rods[i];

            for (int j = 0; j < rod.EdgeScoreProfile.Count; j++)
            {
                if (j < rod.SampleProfile.Occluded.Length &&
                    rod.SampleProfile.Occluded[j])
                {
                    continue;
                }

                max = Math.Max(max, rod.EdgeScoreProfile.Scores[j]);
            }
        }

        return max * GetSquaredRatio();
    }

    private double CalculateMinimumScore(double[] scores, bool[] occludedSamples, int count)
    {
        double max = 0;

        for (int i = 0; i < count; i++)
        {
            if (occludedSamples[i])
            {
                continue;
            }

            max = Math.Max(max, scores[i]);
        }

        return max * GetSquaredRatio();
    }

    private double GetSquaredRatio()
    {
        double ratio = _Options.EdgeMinimumScoreRatio;

        return ratio * ratio;
    }

    private void MarkOccludedSamples(RodColorSampleProfile profile, IReadOnlyList<Trapezium> occlusions)
    {
        Array.Clear(_OccludedSamples, 0, profile.Count);

        if (occlusions.Count == 0)
        {
            return;
        }

        for (int i = 0; i < profile.Count; i++)
        {
            int x = profile.Centers[i].X;
            int y = profile.Centers[i].Y;

            for (int j = 0; j < occlusions.Count; j++)
            {
                if (!IsInsideExpandedOcclusion(x, y, occlusions[j]))
                {
                    continue;
                }

                _OccludedSamples[i] = true;
                break;
            }
        }
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
}
