// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.UnitTests.TableScene;

public class RodEdgePeakIntervalFinderTests
{
    [Fact]
    public void Find_Pairs_Highest_Unused_Peaks_With_Best_Available_Partner()
    {
        double[] scores =
        [
            0, 1, 12, 3, 2, 11, 1, 10, 1,
            0, 13, 3, 2, 12, 1,
        ];

        RodEdgePeakIntervalFinder finder = new(scores.Length);

        RodObjectIntervalScan scan = finder.Find(scores, scores.Length, 4, 1, 4);

        Assert.Equal(2, scan.Count);
        Assert.Equal(new RodObjectInterval(2, 5, scan.Intervals[0].Score), scan.Intervals[0]);
        Assert.Equal(new RodObjectInterval(10, 13, scan.Intervals[1].Score), scan.Intervals[1]);
    }

    [Fact]
    public void Find_Ignores_Peaks_Below_MinimumScore()
    {
        double[] scores =
        [
            0, 1, 12, 3, 2, 11, 1,
            0, 3, 1, 2, 4, 1,
        ];

        RodEdgePeakIntervalFinder finder = new(scores.Length);

        RodObjectIntervalScan scan = finder.Find(scores, scores.Length, 5, 1, 4);

        RodObjectInterval interval = Assert.Single(scan.Intervals.Take(scan.Count));
        Assert.Equal(new RodObjectInterval(2, 5, interval.Score), interval);
    }
}
