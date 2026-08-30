// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using BenchmarkDotNet.Attributes;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Vision;
using FoosVision.Vision.Benchmarks.Android.Tools;
using FoosVision.Vision.TableConfig;
using FoosVision.Vision.TableScene;

namespace FoosVision.Vision.Benchmarks.Android.Benchmarks;

public class VisionContextBenchmarks
{
    private const int _Width = 1920;
    private const int _Height = 1080;

    private byte[]? _RgbaImage;
    private TableConfiguration? _TableConfiguration;
    private TableSceneUpdater? _EncodeUpdater;
    private TableSceneUpdater? _DecodeUpdater;
    private EncodedVisionContext _Context;

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

        var playingField = result.Value;
        _TableConfiguration = new(playingField, new PlayerColors(0xFFFF0000, 0xFF0000FF), BallColor.White);

        TableSceneCalibrator calibrator = new(_Width, _Height);
        var calibration = calibrator.Calibrate(_RgbaImage, playingField);
        ValidateCalibration(calibration);

        TableSceneModel encodeModel = new(_Width, _Height);
        _EncodeUpdater = new(_Width, _Height, encodeModel);
        _EncodeUpdater.ApplyCalibration(calibration);
        _EncodeUpdater.ApplyField(playingField);
        _EncodeUpdater.Update(_RgbaImage, _TableConfiguration, Option<Point>.None());

        if (!_EncodeUpdater.TryGetEncodedVisionContext(out _Context))
        {
            throw new Exception("VisionContext - Encode failed");
        }

        TableSceneModel decodeModel = new(_Width, _Height);
        _DecodeUpdater = new(_Width, _Height, decodeModel);
    }

    [Benchmark]
    public void Encode()
    {
        if (!_EncodeUpdater!.TryGetEncodedVisionContext(out _Context))
        {
            throw new Exception("VisionContext - Encode failed");
        }
    }

    [Benchmark]
    public void Decode()
    {
        if (!_DecodeUpdater!.TryApplyEncodedVisionContext(_Context))
        {
            throw new Exception("VisionContext - Decode failed");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        Console.WriteLine();
        Console.WriteLine($"Vision context size: {_Context.Length} bytes");
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
