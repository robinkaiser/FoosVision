// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using BenchmarkDotNet.Attributes;
using FoosVision.Common.Types;
using FoosVision.Vision.BallFinding.Processing;
using FoosVision.Vision.Benchmarks.Android.Tools;
using FoosVision.Vision.Common.Processing;

namespace FoosVision.Vision.Benchmarks.Android.Benchmarks;

public class BlobFinderBenchmarks
{
    private const int _Width = 1920;
    private const int _Height = 1080;

    private Rectangle _FullImageRect;
    private byte[]? _EdgeImage;
    private BlobFinder? _BlobFinder;
    private Blob[]? _Blobs;
    private int _BlobCount;

    [GlobalSetup]
    public void Setup()
    {
        _FullImageRect = new Rectangle(0, 0, _Width, _Height);

        byte[] rgbImage = DataReader.ReadRGBDataFromAssetsIntoRGBABuffer("Table_Leonhart_100.rgb888", _Width, _Height);
        byte[] gray8Image = new byte[_Width * _Height];

        TableConfig.Processing.ImageTransform.ConvertRGBA8888ToGray8(_Width, rgbImage, gray8Image, _FullImageRect);

        CannyEdgeDetector canny = new(_Width, _Height);
        _EdgeImage = new byte[_Width * _Height];
        canny.Process(gray8Image, _EdgeImage, _FullImageRect);

        BlobFinderParameters param = new()
        {
            MaxBlobCount = 10000,
        };

        _BlobFinder = new(_Width, param);
    }

    [Benchmark]
    public void FindBlobs()
    {
        _BlobCount = _BlobFinder!.ProcessY8(_EdgeImage!, _FullImageRect);
        _Blobs = _BlobFinder.ResultBlobBuffer;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_BlobFinder!.SelfCheck() != SelfCheckStatus.OkPoolFullyFree)
        {
            throw new Exception($"BlobFinder - Selfcheck failed");
        }

        Console.WriteLine();
        Console.WriteLine("Find blobs successful.");
        Console.WriteLine($"Blobs detected: {_BlobCount}");
        Console.WriteLine();

        for (int i = 0; i < _BlobCount; i++)
        {
            Blob blob = _Blobs![i];
            Console.WriteLine($"X0 = {blob.BoundsX0}, Y0 = {blob.BoundsY0}, X1 = {blob.BoundsX1}, Y1 = {blob.BoundsX1}, Count = {blob.PixelCount}");
        }
    }
}
