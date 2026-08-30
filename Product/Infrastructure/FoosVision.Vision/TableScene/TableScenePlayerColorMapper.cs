// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;

namespace FoosVision.Vision.TableScene;

public static class TableScenePlayerColorMapper
{
    private const uint _BlackArgb = 0xFF000000u;

    public static bool TryCreatePlayerColors(
        TableSceneCalibration calibration,
        out PlayerColors playerColors)
    {
        var colorCalibration = calibration.ColoredPlayerColorCalibration;
        var teamA = colorCalibration.TeamA.ColorModel;
        var teamB = colorCalibration.TeamB.ColorModel;

        if (teamA is null &&
            teamB is null)
        {
            playerColors = default;
            return false;
        }

        playerColors = new(
            teamA is null ? _BlackArgb : CreateArgb(teamA),
            teamB is null ? _BlackArgb : CreateArgb(teamB));

        return true;
    }

    private static uint CreateArgb(ChromaticColorModel model)
    {
        const int y = 128;
        int cb = model.CenterCb - 128;
        int cr = model.CenterCr - 128;
        int r = ClampToByte(y + RoundToInt(1.402 * cr));
        int g = ClampToByte(y - RoundToInt((0.344136 * cb) + (0.714136 * cr)));
        int b = ClampToByte(y + RoundToInt(1.772 * cb));

        return 0xFF000000u |
            ((uint)r << 16) |
            ((uint)g << 8) |
            (uint)b;
    }

    private static int ClampToByte(int value)
    {
        if (value < 0)
        {
            return 0;
        }

        return value > 255 ? 255 : value;
    }

    private static int RoundToInt(double value)
        => value >= 0
            ? (int)(value + 0.5)
            : (int)(value - 0.5);
}
