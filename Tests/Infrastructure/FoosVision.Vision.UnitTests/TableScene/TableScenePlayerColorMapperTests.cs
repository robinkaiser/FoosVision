// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Vision.TableScene;
using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;

namespace FoosVision.Vision.UnitTests.TableScene;

public class TableScenePlayerColorMapperTests
{
    [Fact]
    public void TryCreatePlayerColors_Uses_Black_For_Missing_Team_B_Color_Model()
    {
        ChromaticColorModel green = CreateGreenColorModel();
        TableSceneCalibration calibration = CreateCalibration(
            new TeamColorCalibration(Team.A, 5, 5, green),
            new TeamColorCalibration(Team.B, 5, 0, null));

        bool result = TableScenePlayerColorMapper.TryCreatePlayerColors(calibration, out var playerColors);

        Assert.True(result);
        Assert.Equal(0xFF00DD00u, playerColors.TeamAArgb);
        Assert.Equal(0xFF000000u, playerColors.TeamBArgb);
    }

    [Fact]
    public void TryCreatePlayerColors_Uses_Black_For_Missing_Team_A_Color_Model()
    {
        ChromaticColorModel green = CreateGreenColorModel();
        TableSceneCalibration calibration = CreateCalibration(
            new TeamColorCalibration(Team.A, 5, 0, null),
            new TeamColorCalibration(Team.B, 5, 5, green));

        bool result = TableScenePlayerColorMapper.TryCreatePlayerColors(calibration, out var playerColors);

        Assert.True(result);
        Assert.Equal(0xFF000000u, playerColors.TeamAArgb);
        Assert.Equal(0xFF00DD00u, playerColors.TeamBArgb);
    }

    [Fact]
    public void TryCreatePlayerColors_Fails_When_Both_Color_Models_Are_Missing()
    {
        TableSceneCalibration calibration = CreateCalibration(
            new TeamColorCalibration(Team.A, 5, 0, null),
            new TeamColorCalibration(Team.B, 5, 0, null));

        bool result = TableScenePlayerColorMapper.TryCreatePlayerColors(calibration, out var playerColors);

        Assert.False(result);
        Assert.Equal(default, playerColors);
    }

    private static ChromaticColorModel CreateGreenColorModel()
        => new(CenterCb: 54, CenterCr: 34, Radius: 20, MinimumChromaticDistance: 25, SampleCount: 5);

    private static TableSceneCalibration CreateCalibration(
        TeamColorCalibration teamA,
        TeamColorCalibration teamB)
    {
        return new(
            ColoredObjectIntervals: new ColoredRodObjectIntervalDetection([]),
            ColoredPlayerColorCalibration: new ColoredPlayerColorCalibration(teamA, teamB),
            ColoredPlayerMasks: new ColoredPlayerMaskDetection([]),
            BlackObjectIntervals: new BlackRodObjectIntervalDetection([], default),
            BlackObjectMasks: new BlackRodObjectMaskDetection([]));
    }
}
