// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public readonly record struct RodColorEdgeScoreScan(double[] Scores, int Count);

public class RodColorEdgeChangeScorer
{
    private readonly double[] _Scores;

    public RodColorEdgeChangeScorer(int capacity)
    {
        _Scores = new double[capacity];
    }

    public RodColorEdgeScoreScan Calculate(
        ColorFeature[] features,
        int count,
        bool[] occludedSamples,
        int windowLength)
    {
        if (count == 0 ||
            windowLength <= 0)
        {
            return new(_Scores, 0);
        }

        for (int i = 0; i < count; i++)
        {
            if (i < windowLength ||
                i + windowLength > count ||
                ContainsOccludedSample(occludedSamples, i - windowLength, i + windowLength))
            {
                _Scores[i] = 0;
                continue;
            }

            ColorFeature left = CalculateAverage(features, i - windowLength, i);
            ColorFeature right = CalculateAverage(features, i, i + windowLength);
            _Scores[i] = ColorFeature.GetSquaredDistance(left, right);
        }

        return new(_Scores, count);
    }

    private static ColorFeature CalculateAverage(ColorFeature[] features, int start, int stop)
    {
        int sumCb = 0;
        int sumCr = 0;

        for (int i = start; i < stop; i++)
        {
            sumCb += features[i].Cb;
            sumCr += features[i].Cr;
        }

        int count = stop - start;

        return new(
            sumCb / count,
            sumCr / count);
    }

    private static bool ContainsOccludedSample(bool[] occludedSamples, int start, int stop)
    {
        for (int i = start; i < stop; i++)
        {
            if (occludedSamples[i])
            {
                return true;
            }
        }

        return false;
    }
}
