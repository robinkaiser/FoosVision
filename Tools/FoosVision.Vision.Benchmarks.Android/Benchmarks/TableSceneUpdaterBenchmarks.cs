// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using BenchmarkDotNet.Attributes;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.Benchmarks.Android.Tools;
using FoosVision.Vision.TableConfig;
using FoosVision.Vision.TableScene;

namespace FoosVision.Vision.Benchmarks.Android.Benchmarks;

public class TableSceneUpdaterBenchmarks
{
    private const int _Width = 1920;
    private const int _Height = 1080;

    private byte[]? _RgbaImage;
    private PlayingField? _Field;
    private TableConfiguration? _TableConfiguration;

    private TableSceneUpdater? _Updater;
    private TableSceneUpdater? _UpdaterWithAppliedField;

    [GlobalSetup]
    public void Setup()
    {
        _RgbaImage = DataReader.ReadRGBDataFromAssetsIntoRGBABuffer("Table_Leonhart_100.rgb888", _Width, _Height);

        FieldDetector fieldDetector = new(_Width, _Height);
        var result = fieldDetector.Detect(_RgbaImage);

        if (!result.HasValue)
        {
            throw new Exception($"FieldDetector - Detect failed");
        }

        _Field = result.Value;
        _TableConfiguration = new(_Field, new PlayerColors(0xFFFF0000, 0xFF0000FF), BallColor.White);

        TableSceneCalibrator calibrator = new(_Width, _Height);
        var calibration = calibrator.Calibrate(_RgbaImage, _Field);

        if (!calibration.ColoredPlayerColorCalibration.TeamA.HasColorModel ||
            !calibration.ColoredPlayerColorCalibration.TeamB.HasColorModel)
        {
            throw new Exception("TableSceneCalibrator - Expected two color models");
        }

        _Updater = new(_Width, _Height, new(_Width, _Height));
        _Updater.ApplyCalibration(calibration);

        _UpdaterWithAppliedField = new(_Width, _Height, new(_Width, _Height));
        _UpdaterWithAppliedField.ApplyCalibration(calibration);
        _UpdaterWithAppliedField.ApplyField(_Field);
    }

    [Benchmark]
    public void ApplyField()
    {
        _Updater!.ApplyField(_Field!);
    }

    [Benchmark]
    public void Update()
    {
        _UpdaterWithAppliedField!.Update(_RgbaImage!, _TableConfiguration!, Option<Point>.None());
    }
}
