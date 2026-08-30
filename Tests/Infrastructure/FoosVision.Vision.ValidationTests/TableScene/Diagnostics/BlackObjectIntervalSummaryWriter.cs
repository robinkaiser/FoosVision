// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.BlackObjects;

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class BlackObjectIntervalSummaryWriter
{
    public static void Write(BlackRodObjectIntervalDetection detection, string outputPath)
    {
        List<string> lines =
        [
            "Black object interval detection",
            string.Empty,
            $"MaximumObjectY: {detection.Rule.MaximumObjectY}",
            $"ObjectPercentile: {detection.Rule.ObjectPercentile:0.00}",
            $"PercentileObjectY: {detection.Rule.PercentileObjectY}",
            $"SearchMinimumPercentile: {detection.Rule.SearchMinimumPercentile:0.00}",
            $"SearchMaximumPercentile: {detection.Rule.SearchMaximumPercentile:0.00}",
            $"SearchMinimumY: {detection.Rule.SearchMinimumY}",
            $"SearchMaximumY: {detection.Rule.SearchMaximumY}",
            "ThresholdMethod: LocalOtsu",
            $"SideBandOffset: {detection.Rule.SideBandOffset}",
            $"SideBandWidth: {detection.Rule.SideBandWidth}",
            $"MinimumRunLength: {detection.Rule.MinimumRunLength}",
            $"MaximumGapLength: {detection.Rule.MaximumGapLength}",
            string.Empty,
        ];

        foreach (var rod in detection.Rods)
        {
            int validLeft = CountValid(rod.SampleProfile.LeftValid, rod.SampleProfile.Count);
            int validRight = CountValid(rod.SampleProfile.RightValid, rod.SampleProfile.Count);
            int matches = CountValid(rod.SampleProfile.Matches, rod.SampleProfile.Count);

            lines.Add($"{rod.BarType}: samples={rod.SampleProfile.Count}, leftValid={validLeft}, rightValid={validRight}, matches={matches}, intervals={rod.Intervals.Count}");

            foreach (var interval in rod.Intervals)
            {
                lines.Add($"  {interval.StartIndex}-{interval.EndIndex}, length={interval.Length}, score={interval.Score:0.0}");
            }
        }

        File.WriteAllLines(outputPath, lines);
    }

    private static int CountValid(bool[] values, int count)
    {
        int result = 0;

        for (int i = 0; i < count; i++)
        {
            if (values[i])
            {
                result++;
            }
        }

        return result;
    }
}
