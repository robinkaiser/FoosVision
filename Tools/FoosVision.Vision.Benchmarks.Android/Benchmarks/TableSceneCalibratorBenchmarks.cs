// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using BenchmarkDotNet.Attributes;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.Benchmarks.Android.Tools;
using FoosVision.Vision.TableConfig;
using FoosVision.Vision.TableScene;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;

namespace FoosVision.Vision.Benchmarks.Android.Benchmarks;

public class TableSceneCalibratorBenchmarks
{
    private const int _Width = 1920;
    private const int _Height = 1080;

    private byte[]? _RgbaImage;
    private PlayingField? _Field;
    private TableSceneCalibrator? _Calibrator;
    private TableSceneCalibration? _Calibration;

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
        _Calibrator = new(_Width, _Height);
        _Calibration = _Calibrator.Calibrate(_RgbaImage, _Field);
        ValidateCalibration(_Calibration);
    }

    [Benchmark]
    public void Calibrate()
    {
        _Calibration = _Calibrator!.Calibrate(_RgbaImage!, _Field!);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        ValidateCalibration(_Calibration);

        Console.WriteLine();
        Console.WriteLine("TableScene calibration completed.");
        PrintTeamCalibration("TeamA", _Calibration!.ColoredPlayerColorCalibration.TeamA);
        PrintTeamCalibration("TeamB", _Calibration.ColoredPlayerColorCalibration.TeamB);
        Console.WriteLine($"BlackObjectMaximumY = {_Calibration.BlackObjectIntervals.Rule.MaximumObjectY}");
    }

    private static void ValidateCalibration(TableSceneCalibration? calibration)
    {
        if (calibration is null)
        {
            throw new Exception("TableSceneCalibrator - Calibration missing");
        }

        if (!calibration.ColoredPlayerColorCalibration.TeamA.HasColorModel ||
            !calibration.ColoredPlayerColorCalibration.TeamB.HasColorModel)
        {
            throw new Exception("TableSceneCalibrator - Expected two color models");
        }
    }

    private static void PrintTeamCalibration(string label, TeamColorCalibration calibration)
    {
        ChromaticColorModel model = calibration.ColorModel ??
            throw new Exception($"TableSceneCalibrator - {label} color model missing");

        Console.WriteLine(
            $"{label}: Team = {calibration.Team}, " +
            $"Intervals = {calibration.IntervalCount}, " +
            $"ChromaticSamples = {calibration.ChromaticSampleCount}, " +
            $"HasColorModel = {calibration.HasColorModel}, " +
            $"CenterCb = {model.CenterCb}, " +
            $"CenterCr = {model.CenterCr}, " +
            $"Radius = {model.Radius}, " +
            $"MinimumChromaticDistance = {model.MinimumChromaticDistance}, " +
            $"ModelSamples = {model.SampleCount}");
    }
}
