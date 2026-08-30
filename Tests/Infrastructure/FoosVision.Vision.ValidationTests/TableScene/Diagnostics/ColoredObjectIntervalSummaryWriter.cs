// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Globalization;
using System.Text;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;
namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class ColoredObjectIntervalSummaryWriter
{
    public static void Write(ColoredRodObjectIntervalDetection detection, string outputPath)
    {
        StringBuilder sb = new();

        foreach (var rod in detection.Rods)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{rod.BarType}: ");
            sb.Append(CultureInfo.InvariantCulture, $"EdgeMax={GetMaxEdgeScore(rod):0.000}; ");
            sb.Append(CultureInfo.InvariantCulture, $"EdgeMinimum={rod.EdgeScoreProfile.MinimumScore:0.000}; ");
            sb.Append(CultureInfo.InvariantCulture, $"OccludedSamples={GetOccludedSampleCount(rod)}; ");
            sb.Append(CultureInfo.InvariantCulture, $"Intervals={rod.Intervals.Count}");
            sb.AppendLine();

            foreach (var interval in rod.Intervals)
            {
                sb.Append(CultureInfo.InvariantCulture, $"  [{interval.StartIndex}, {interval.EndIndex}] ");
                sb.Append(CultureInfo.InvariantCulture, $"Length={interval.Length}; Score={interval.Score:0.000}");
                sb.AppendLine();
            }
        }

        File.WriteAllText(outputPath, sb.ToString());
    }

    private static double GetMaxEdgeScore(RodColoredObjectIntervals rod)
    {
        double max = 0;

        for (int i = 0; i < rod.EdgeScoreProfile.Count; i++)
        {
            max = Math.Max(max, rod.EdgeScoreProfile.Scores[i]);
        }

        return max;
    }

    private static int GetOccludedSampleCount(RodColoredObjectIntervals rod)
    {
        int count = 0;

        for (int i = 0; i < rod.SampleProfile.Count; i++)
        {
            if (rod.SampleProfile.Occluded[i])
            {
                count++;
            }
        }

        return count;
    }
}
