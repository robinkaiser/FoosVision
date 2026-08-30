// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.UnitTests.TableScene;

public class ColoredPlayerColorModelCalibratorTests
{
    [Fact]
    public void Calibrate_Builds_Team_Color_Models_From_Five_Intervals()
    {
        ColorFeature red = ColorFeature.FromRgb(220, 20, 20);
        ColorFeature blue = ColorFeature.FromRgb(20, 40, 220);
        ColoredRodObjectIntervalDetection detection = new(
            [
                CreateRod(BarType.A1, red, red),
                CreateRod(BarType.A2),
                CreateRod(BarType.B3, blue, blue, blue),
                CreateRod(BarType.A5),
                CreateRod(BarType.B5),
                CreateRod(BarType.A3, red, red, red),
                CreateRod(BarType.B2, blue, blue),
                CreateRod(BarType.B1),
            ]);
        ColoredPlayerColorModelCalibrator calibrator = new();

        ColoredPlayerColorCalibration calibration = calibrator.Calibrate(detection);

        Assert.True(calibration.TeamA.HasColorModel);
        Assert.True(calibration.TeamB.HasColorModel);
        Assert.Equal(Team.A, calibration.TeamA.Team);
        Assert.Equal(Team.B, calibration.TeamB.Team);
        Assert.Equal(5, calibration.TeamA.IntervalCount);
        Assert.Equal(5, calibration.TeamB.IntervalCount);
        Assert.Equal(red.Cb, calibration.TeamA.ColorModel!.CenterCb);
        Assert.Equal(red.Cr, calibration.TeamA.ColorModel.CenterCr);
        Assert.Equal(blue.Cb, calibration.TeamB.ColorModel!.CenterCb);
        Assert.Equal(blue.Cr, calibration.TeamB.ColorModel.CenterCr);
    }

    [Fact]
    public void Calibrate_Does_Not_Build_Color_Model_Below_Five_Intervals()
    {
        ColorFeature green = ColorFeature.FromRgb(20, 160, 40);
        ColoredRodObjectIntervalDetection detection = new(
            [
                CreateRod(BarType.A1, green, green),
                CreateRod(BarType.A2),
                CreateRod(BarType.B3),
                CreateRod(BarType.A5),
                CreateRod(BarType.B5),
                CreateRod(BarType.A3, green, green),
                CreateRod(BarType.B2),
                CreateRod(BarType.B1),
            ]);
        ColoredPlayerColorModelCalibrator calibrator = new();

        ColoredPlayerColorCalibration calibration = calibrator.Calibrate(detection);

        Assert.False(calibration.TeamA.HasColorModel);
        Assert.Equal(4, calibration.TeamA.IntervalCount);
        Assert.Equal(0, calibration.TeamA.ChromaticSampleCount);
    }

    [Fact]
    public void Calibrate_Ignores_Achromatic_Samples_When_Building_Model()
    {
        ColorFeature red = ColorFeature.FromRgb(220, 20, 20);
        ColorFeature white = ColorFeature.FromRgb(245, 245, 245);
        RodColoredObjectIntervals rod = CreateRod(
            BarType.A3,
            [
                red,
                white,
                red,
                white,
                red,
                white,
                red,
                white,
                red,
                white,
            ],
            [
                new(0, 1, 1),
                new(2, 3, 1),
                new(4, 5, 1),
                new(6, 7, 1),
                new(8, 9, 1),
            ]);
        ColoredRodObjectIntervalDetection detection = new(
            [
                CreateRod(BarType.A1),
                CreateRod(BarType.A2),
                CreateRod(BarType.B3),
                CreateRod(BarType.A5),
                CreateRod(BarType.B5),
                rod,
                CreateRod(BarType.B2),
                CreateRod(BarType.B1),
            ]);
        ColoredPlayerColorModelCalibrator calibrator = new();

        ColoredPlayerColorCalibration calibration = calibrator.Calibrate(detection);

        Assert.True(calibration.TeamA.HasColorModel);
        Assert.Equal(5, calibration.TeamA.IntervalCount);
        Assert.Equal(5, calibration.TeamA.ChromaticSampleCount);
        Assert.Equal(red.Cb, calibration.TeamA.ColorModel!.CenterCb);
        Assert.Equal(red.Cr, calibration.TeamA.ColorModel.CenterCr);
    }

    private static RodColoredObjectIntervals CreateRod(BarType type, params ColorFeature[] features)
    {
        RodObjectInterval[] intervals = new RodObjectInterval[features.Length];

        for (int i = 0; i < intervals.Length; i++)
        {
            intervals[i] = new(i, i, 1);
        }

        return CreateRod(type, features, intervals);
    }

    private static RodColoredObjectIntervals CreateRod(
        BarType type,
        ColorFeature[] features,
        RodObjectInterval[] intervals)
    {
        int[] x = new int[features.Length];
        int[] y = new int[features.Length];
        bool[] occluded = new bool[features.Length];

        for (int i = 0; i < features.Length; i++)
        {
            x[i] = i;
            y[i] = 3;
        }

        return new(
            type,
            intervals,
            new(x, y, occluded, features, features.Length),
            new([], 0, 0));
    }
}
