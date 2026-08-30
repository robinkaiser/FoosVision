// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.TableScene.Processing.Common;

public class RodEdgePeakIntervalFinder
{
    private readonly int[] _PeakIndexes;
    private readonly RodObjectInterval[] _SelectedIntervals;
    private readonly bool[] _PeakUsed;

    public RodEdgePeakIntervalFinder(int capacity)
    {
        _PeakIndexes = new int[capacity];
        _SelectedIntervals = new RodObjectInterval[capacity];
        _PeakUsed = new bool[capacity];
    }

    public RodObjectIntervalScan Find(
        double[] scores,
        int count,
        double minimumScore,
        int edgePeakNeighborhood,
        int edgePairMaximumDistance)
    {
        if (count == 0)
        {
            return new(_SelectedIntervals, 0);
        }

        int peakCount = FindPeaks(scores, count, minimumScore, edgePeakNeighborhood);
        int selectedCount = PairPeaks(scores, peakCount, edgePairMaximumDistance);
        SortSelectedIntervals(selectedCount);

        return new(_SelectedIntervals, selectedCount);
    }

    private int FindPeaks(double[] scores, int count, double minimumScore, int neighborhood)
    {
        int peakCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (scores[i] < minimumScore ||
                !IsLocalMaximum(scores, count, i, neighborhood))
            {
                continue;
            }

            _PeakIndexes[peakCount] = i;
            peakCount++;
        }

        return peakCount;
    }

    private static bool IsLocalMaximum(double[] scores, int count, int index, int neighborhood)
    {
        double score = scores[index];
        int start = Math.Max(0, index - neighborhood);
        int stop = Math.Min(count - 1, index + neighborhood);

        for (int i = start; i <= stop; i++)
        {
            if (i == index)
            {
                continue;
            }

            if (scores[i] > score ||
                (i < index && scores[i] == score))
            {
                return false;
            }
        }

        return true;
    }

    private int PairPeaks(double[] scores, int peakCount, int maximumDistance)
    {
        Array.Clear(_PeakUsed, 0, peakCount);

        int selectedCount = 0;

        while (true)
        {
            int peakIndex = FindHighestUnusedPeak(scores, peakCount);

            if (peakIndex < 0)
            {
                return selectedCount;
            }

            int partnerIndex = FindBestPartner(scores, peakCount, peakIndex, maximumDistance);
            _PeakUsed[peakIndex] = true;

            if (partnerIndex < 0)
            {
                continue;
            }

            _PeakUsed[partnerIndex] = true;

            int start = Math.Min(_PeakIndexes[peakIndex], _PeakIndexes[partnerIndex]);
            int end = Math.Max(_PeakIndexes[peakIndex], _PeakIndexes[partnerIndex]);
            double score = Math.Min(scores[_PeakIndexes[peakIndex]], scores[_PeakIndexes[partnerIndex]]);

            _SelectedIntervals[selectedCount] = new(start, end, score);
            selectedCount++;
        }
    }

    private int FindHighestUnusedPeak(double[] scores, int peakCount)
    {
        int bestIndex = -1;
        double bestScore = double.MinValue;

        for (int i = 0; i < peakCount; i++)
        {
            if (_PeakUsed[i])
            {
                continue;
            }

            double score = scores[_PeakIndexes[i]];

            if (score < bestScore)
            {
                continue;
            }

            bestIndex = i;
            bestScore = score;
        }

        return bestIndex;
    }

    private int FindBestPartner(double[] scores, int peakCount, int peakIndex, int maximumDistance)
    {
        int bestIndex = -1;
        double bestScore = double.MinValue;
        int bestDistance = int.MaxValue;
        int x = _PeakIndexes[peakIndex];

        for (int i = 0; i < peakCount; i++)
        {
            if (i == peakIndex ||
                _PeakUsed[i])
            {
                continue;
            }

            int distance = Math.Abs(_PeakIndexes[i] - x);

            if (distance > maximumDistance)
            {
                continue;
            }

            double score = scores[_PeakIndexes[i]];

            if (score < bestScore ||
                (score == bestScore && distance >= bestDistance))
            {
                continue;
            }

            bestIndex = i;
            bestScore = score;
            bestDistance = distance;
        }

        return bestIndex;
    }

    private void SortSelectedIntervals(int count)
    {
        for (int i = 1; i < count; i++)
        {
            RodObjectInterval value = _SelectedIntervals[i];
            int j = i - 1;

            while (j >= 0 && _SelectedIntervals[j].StartIndex > value.StartIndex)
            {
                _SelectedIntervals[j + 1] = _SelectedIntervals[j];
                j--;
            }

            _SelectedIntervals[j + 1] = value;
        }
    }
}
