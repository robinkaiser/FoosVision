// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.TableConfig.Processing;

public record struct PfPoint(double X0, double X1);

public record struct PfPointSequence(int[] Indices, double Error);

public static class EquallySpacedPointsFinder
{
    private const double _MinimumPointDistance = 100.0;

    public static List<PfPointSequence> SelectSequences(PfPoint[] points, int n, int numPoints, int maxSequenceCount)
    {
        if (n < numPoints || maxSequenceCount <= 0)
        {
            return [];
        }

        List<PfPointSequence> sequences = [];

        // Try every possible pair of endpoints.
        // i0: candidate index for the first selected point.
        // iLast: candidate index for the last selected point.
        // We ensure that the candidate pair has at least numPoints elements between them.
        for (int i0 = 0; i0 <= n - numPoints; i0++)
        {
            for (int iLast = i0 + numPoints - 1; iLast < n; iLast++)
            {
                if (!HasMinimumSpan(points[i0], points[iLast], numPoints))
                {
                    continue;
                }

                // Compute the ideal step for an arithmetic progression between the candidate endpoints.
                double d0 = (points[iLast].X0 - points[i0].X0) / (numPoints - 1);
                double d1 = (points[iLast].X1 - points[i0].X1) / (numPoints - 1);
                List<int> currentSequence = [i0];
                int lastIndex = i0;
                double totalError = 0;
                bool valid = true;

                // For each intermediate slot r = 1 ... numPoints-2,
                // find (greedily) the point (between lastIndex and iLast)
                // that is closest to the ideal target = points[i0] + r*d.
                for (int r = 1; r < numPoints - 1; r++)
                {
                    double target0 = points[i0].X0 + (r * d0);
                    double target1 = points[i0].X1 + (r * d1);
                    int bestCandidate = -1;
                    double bestDiff = double.MaxValue;

                    // We must leave enough room for the remaining selections.
                    // j runs only until iLast - (numPoints - 1 - r)
                    for (int j = lastIndex + 1; j <= iLast - (numPoints - 1 - r); j++)
                    {
                        if (!HasMinimumDistance(points[lastIndex], points[j]))
                        {
                            continue;
                        }

                        double diff = Math.Abs(points[j].X0 - target0) + Math.Abs(points[j].X1 - target1);
                        if (diff < bestDiff)
                        {
                            bestDiff = diff;
                            bestCandidate = j;
                        }

                        // Since the list is sorted, once we pass the target, differences will only increase.
                        if (bestCandidate != -1 && points[j].X0 > target0)
                        {
                            break;
                        }
                    }
                    if (bestCandidate == -1)
                    {
                        valid = false;
                        break;
                    }
                    currentSequence.Add(bestCandidate);
                    totalError += bestDiff;
                    lastIndex = bestCandidate;
                }

                if (!valid) continue;

                if (!HasMinimumDistance(points[lastIndex], points[iLast]))
                {
                    continue;
                }

                // Add the candidate endpoint.
                currentSequence.Add(iLast);

                sequences.Add(new([.. currentSequence], totalError));
            }
        }

        return sequences
            .OrderBy(sequence => sequence.Error)
            .Take(maxSequenceCount)
            .ToList();
    }

    private static bool HasMinimumSpan(PfPoint first, PfPoint last, int numPoints)
    {
        double minimumSpan = _MinimumPointDistance * (numPoints - 1);
        return Math.Abs(last.X0 - first.X0) >= minimumSpan
            && Math.Abs(last.X1 - first.X1) >= minimumSpan;
    }

    private static bool HasMinimumDistance(PfPoint previous, PfPoint current)
    {
        return Math.Abs(current.X0 - previous.X0) >= _MinimumPointDistance
            && Math.Abs(current.X1 - previous.X1) >= _MinimumPointDistance;
    }
}
