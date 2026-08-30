// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using BenchmarkDotNet.Attributes;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Vision.BallFinding;
using FoosVision.Vision.Benchmarks.Android.Tools;
using FoosVision.Vision.TableConfig;
using FoosVision.Vision.TableScene;

namespace FoosVision.Vision.Benchmarks.Android.Benchmarks;

public class BallFinderBenchmarks
{
    private const int _Width = 1920;
    private const int _Height = 1080;

    private byte[]? _RgbaImage100;
    private byte[]? _RgbaImage500;
    private TableConfiguration? _TableConfiguration;

    private BallFinder? _BallFinder;
    private IReadOnlyList<ObservedBall>? _Balls;

    [GlobalSetup]
    public void Setup()
    {
        _RgbaImage100 = DataReader.ReadRGBDataFromAssetsIntoRGBABuffer("Table_Leonhart_100.rgb888", _Width, _Height);
        _RgbaImage500 = DataReader.ReadRGBDataFromAssetsIntoRGBABuffer("Table_Leonhart_500.rgb888", _Width, _Height);

        FieldDetector fieldDetector = new(_Width, _Height);
        var result = fieldDetector.Detect(_RgbaImage100);

        if (!result.HasValue)
        {
            throw new Exception($"FieldDetector - Detect failed");
        }

        var playingField = result.Value;
        _TableConfiguration = new(playingField, new PlayerColors(0xFFFF0000, 0xFF0000FF), BallColor.White);

        TableSceneCalibrator calibrator = new(_Width, _Height);
        var calibration = calibrator.Calibrate(_RgbaImage100, playingField);
        ValidateCalibration(calibration);

        TableSceneModel model = new(_Width, _Height);
        TableSceneUpdater updater = new(_Width, _Height, model);
        updater.ApplyCalibration(calibration);
        updater.ApplyField(playingField);
        updater.Update(_RgbaImage100, _TableConfiguration!, Option<Point>.None());

        _BallFinder = new(_Width, _Height, updater, 50);
    }

    [Benchmark]
    public void Detect()
    {
        _Balls = _BallFinder!.Detect(_RgbaImage500!, _TableConfiguration!);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Console.WriteLine();
        Console.WriteLine("Detect successful.");
        Console.WriteLine($"Balls detected: {_Balls!.Count}");

        foreach (var ball in _Balls!)
        {
            Console.WriteLine($"X = {ball.Position.X}, Y = {ball.Position.Y}, Quality = {ball.Quality}");
        }

        var realBall = _Balls.MaxBy(b => b.Quality)!;
        Console.WriteLine();
        Console.WriteLine($"Real ball: X = {realBall.Position.X}, Y = {realBall.Position.Y}, Quality = {realBall.Quality}");

        var dX = Math.Abs(realBall.Position.X - 633);
        var dY = Math.Abs(realBall.Position.Y - 614);

        if (dX > 10.0 || dY > 10.0)
        {
            throw new Exception($"BallFinder - Real ball not found");
        }
        else
        {
            Console.WriteLine($"- within parameters: dX = {dX}, dY = {dY}");
        }
    }

    private static void ValidateCalibration(TableSceneCalibration calibration)
    {
        if (!calibration.ColoredPlayerColorCalibration.TeamA.HasColorModel ||
            !calibration.ColoredPlayerColorCalibration.TeamB.HasColorModel)
        {
            throw new Exception("TableSceneCalibrator - Expected two color models");
        }
    }
}
