// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using BenchmarkDotNet.Attributes;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.Benchmarks.Android.Tools;
using FoosVision.Vision.TableConfig;

namespace FoosVision.Vision.Benchmarks.Android.Benchmarks;

public class FieldDetectorBenchmarks
{
    private const int _Width = 1920;
    private const int _Height = 1080;

    private FieldDetector? _FieldDetector;
    private byte[]? _RgbaImage;

    private PlayingField? _LastField;

    [GlobalSetup]
    public void Setup()
    {
        _RgbaImage = DataReader.ReadRGBDataFromAssetsIntoRGBABuffer("Table_Leonhart_100.rgb888", _Width, _Height);

        _FieldDetector = new(_Width, _Height);
    }

    [Benchmark]
    public void FieldDetector()
    {
        var result = _FieldDetector!.Detect(_RgbaImage!);

        if (!result.HasValue)
        {
            throw new Exception($"FieldDetector - Detect failed");
        }

        _LastField = result.Value;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_LastField == null)
        {
            throw new Exception($"FieldDetector - Cleanup - no field");
        }

        var bars = _LastField.Bars;
        var border = _LastField.Boundary;

        Console.WriteLine();
        Console.WriteLine("Field successfully detected:");

        foreach (var bar in bars.All)
        {
            Console.WriteLine($"Bar: {bar.Type} " +
                $" - Left: ({bar.Left.P0.X:0}, {bar.Left.P0.Y:0}), ({bar.Left.P1.X:0}, {bar.Left.P1.Y:0})" +
                $" - Center: ({bar.Center.P0.X:0}, {bar.Center.P0.Y:0}), ({bar.Center.P1.X:0}, {bar.Center.P1.Y:0})" +
                $" - Right: ({bar.Right.P0.X:0}, {bar.Right.P0.Y:0}), ({bar.Right.P1.X:0}, {bar.Right.P1.Y:0})");
        }

        Console.WriteLine($"Border: " +
            $"({border.UpperLeft.X:0}, {border.UpperLeft.Y:0}), ({border.UpperRight.X:0}, {border.UpperRight.Y:0}), " +
            $"({border.LowerLeft.X:0}, {border.LowerLeft.Y:0}), ({border.LowerRight.X:0}, {border.LowerRight.Y:0})");
    }
}
