// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Globalization;
using System.Text;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;
namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class ColorModelSummaryWriter
{
    public static void Write(ColoredPlayerColorCalibration calibration, string outputPath)
    {
        StringBuilder sb = new();

        AppendTeam(sb, calibration.TeamA);
        AppendTeam(sb, calibration.TeamB);

        File.WriteAllText(outputPath, sb.ToString());
    }

    private static void AppendTeam(StringBuilder sb, TeamColorCalibration team)
    {
        sb.Append(CultureInfo.InvariantCulture, $"{team.Team}: ");
        sb.Append(CultureInfo.InvariantCulture, $"Intervals={team.IntervalCount}; ");
        sb.Append(CultureInfo.InvariantCulture, $"ChromaticSamples={team.ChromaticSampleCount}; ");

        if (team.ColorModel is null)
        {
            sb.Append("ColorModel=None");
            sb.AppendLine();
            return;
        }

        ChromaticColorModel model = team.ColorModel;
        sb.Append(CultureInfo.InvariantCulture, $"CenterCb={model.CenterCb}; ");
        sb.Append(CultureInfo.InvariantCulture, $"CenterCr={model.CenterCr}; ");
        sb.Append(CultureInfo.InvariantCulture, $"Radius={model.Radius:0.000}; ");
        sb.Append(CultureInfo.InvariantCulture, $"MinimumChromaticDistance={model.MinimumChromaticDistance}");
        sb.AppendLine();
    }
}
