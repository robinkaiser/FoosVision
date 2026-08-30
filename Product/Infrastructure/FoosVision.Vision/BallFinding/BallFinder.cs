// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Vision.BallFinding.Processing;
using FoosVision.Vision.BallFinding.Processing.CircleFinding;
using FoosVision.Vision.Common;
using FoosVision.Vision.Common.Processing;

namespace FoosVision.Vision.BallFinding;

/// <summary>
/// Detects balls in the given frame buffer.
/// </summary>
/// <remarks>
/// This implementation is not thread-safe. Do not share a single instance of <see cref="BallFinder"/>
/// across multiple threads or use it for parallel detections. If concurrent detection is required,
/// create a separate <see cref="BallFinder"/> instance per thread or per processing pipeline.
/// </remarks>
public class BallFinder
{
    private readonly int _Width;
    private readonly int _Height;
    private readonly IBallDetectionContextProvider _BallDetectionContextProvider;
    private readonly byte[] _Y8ImageBuffer;
    private readonly byte[] _Rgba8888ImageBuffer;
    private readonly EdgePoint[] _EdgePointBuffer;

    private readonly BlobFinder _BlobFinder;
    private readonly CannyEdgeDetector _CannyEdgeDetector;

    private readonly CirclePartCalculator _CirclePartCalculator;
    private readonly CirclePartsMerger _CirclePartsMerger;
    private readonly int _ExpectedRadius;
    private readonly int _MaxCircleParts;

    public BallFinder(int width, int height, IBallDetectionContextProvider ballDetectionContextProvider, int maxCircleParts)
    {
        _Width = width;
        _Height = height;
        _BallDetectionContextProvider = ballDetectionContextProvider;
        _Y8ImageBuffer = new byte[width * height];
        _Rgba8888ImageBuffer = new byte[width * height * 4];
        _EdgePointBuffer = new EdgePoint[width * height];
        _MaxCircleParts = maxCircleParts;

        _BlobFinder = new(width, BlobFinderParameters.Default);
        _CannyEdgeDetector = new CannyEdgeDetector(width, height);

        var circlePartCalculatorParams = CirclePartCalculatorParameters.Default;
        _CirclePartCalculator = new(circlePartCalculatorParams);
        _ExpectedRadius = circlePartCalculatorParams.ExpectedRadius;
        _CirclePartsMerger = new(_ExpectedRadius, _MaxCircleParts);
    }

    public byte[] BallDetectionMask => _Y8ImageBuffer;

    public IReadOnlyList<ObservedBall> Detect(byte[] frameBufferRGBA8888, TableConfiguration tableConfig)
    {
        Rectangle playingFieldRect = GetPlayingFieldRect(tableConfig.Field);

        return Detect(frameBufferRGBA8888, tableConfig, playingFieldRect);
    }

    public IReadOnlyList<ObservedBall> Detect(byte[] frameBufferRGBA8888, TableConfiguration tableConfig, Rectangle regionOfInterest)
    {
        Rectangle playingFieldRect = GetPlayingFieldRect(tableConfig.Field);
        Rectangle processingRect = Rectangle.Intersect(playingFieldRect, regionOfInterest);

        if (processingRect.IsEmpty ||
            processingRect.Width < 3 ||
            processingRect.Height < 3)
        {
            return [];
        }

        ImageTransform.ConvertRGBA8888toGray8(
            _Width,
            frameBufferRGBA8888,
            _BallDetectionContextProvider.ColorResponse32bpp,
            _Y8ImageBuffer,
            processingRect,
            IBallDetectionContextProvider.IgnoredPixel,
            tableConfig.Ball,
            _BallDetectionContextProvider.PlayerColorExclusion);

        int circlePartCount = AccumulateCircleParts(processingRect);

        List<ObservedBall> balls = BuildObservedBalls(circlePartCount, processingRect);

        return balls;
    }

    public IReadOnlyList<ObservedBall> DetectYuv420(
        byte[] bufferY,
        byte[] bufferU,
        byte[] bufferV,
        int width,
        int height,
        int yRowStride,
        int yPixelStride,
        int uRowStride,
        int uPixelStride,
        int vRowStride,
        int vPixelStride,
        TableConfiguration tableConfig,
        Rectangle regionOfInterest)
    {
        Rectangle playingFieldRect = GetPlayingFieldRect(tableConfig.Field);
        Rectangle processingRect = Rectangle.Intersect(playingFieldRect, regionOfInterest);

        if (processingRect.IsEmpty ||
            processingRect.Width < 3 ||
            processingRect.Height < 3)
        {
            return [];
        }

        ImageTransform.ConvertYuv420ToRGBA8888(
            bufferY,
            bufferU,
            bufferV,
            width,
            yRowStride,
            yPixelStride,
            uRowStride,
            uPixelStride,
            vRowStride,
            vPixelStride,
            _Rgba8888ImageBuffer,
            processingRect);

        return Detect(_Rgba8888ImageBuffer, tableConfig, processingRect);
    }

    private static Rectangle GetPlayingFieldRect(PlayingField playingField)
    {
        var boundary = playingField.Boundary;

        var x0 = (int)Math.Min(boundary.UpperLeft.X, boundary.LowerLeft.X);
        var y0 = (int)Math.Min(boundary.UpperLeft.Y, boundary.UpperRight.Y);
        var x1 = (int)(Math.Max(boundary.UpperRight.X, boundary.LowerRight.X) + 0.5);
        var y1 = (int)(Math.Max(boundary.LowerLeft.Y, boundary.LowerRight.Y) + 0.5);

        return new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    private int AccumulateCircleParts(Rectangle processingRect)
    {
        int blobCount = _BlobFinder.ProcessY8(_Y8ImageBuffer, processingRect);
        Blob[] blobs = _BlobFinder.ResultBlobBuffer;
        int circlePartCount = 0;

        for (int blobIndex = 0; blobIndex < blobCount; blobIndex++)
        {
            Blob blob = blobs[blobIndex];
            var x0 = blob.BoundsX0 - 1;
            var y0 = blob.BoundsY0 - 1;
            var x1 = blob.BoundsX1 + 1;
            var y1 = blob.BoundsY1 + 1;

            Rectangle blobRect = new(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
            Rectangle clippedBlobRect = Rectangle.Intersect(blobRect, processingRect);

            if (clippedBlobRect.Width < 3 ||
                clippedBlobRect.Height < 3)
            {
                continue;
            }

            int edgePointCount = _CannyEdgeDetector.Process(_Y8ImageBuffer, clippedBlobRect, _EdgePointBuffer);
            _CirclePartCalculator.ProcessEdges(
                _EdgePointBuffer,
                edgePointCount,
                _CirclePartsMerger.CirclePartBuffer,
                ref circlePartCount);

            if (circlePartCount == _MaxCircleParts) break;
        }

        return circlePartCount;
    }

    private List<ObservedBall> BuildObservedBalls(int circlePartCount, Rectangle processingRect)
    {
        var mergedCircles = _CirclePartsMerger.MergeCircles(circlePartCount);
        List<ObservedBall> balls = [];

        foreach (var circle in mergedCircles)
        {
            int x = circle.X;
            int y = circle.Y;
            int radius = _ExpectedRadius;
            int diameter = radius * 2;

            Rectangle ballRect = new(x - radius, y - radius, diameter, diameter);
            Rectangle clippedBallRect = Rectangle.Intersect(ballRect, processingRect);
            int pixelCount = clippedBallRect.IsEmpty
                ? 0
                : ImageStatistics.CountNonZeroGray8(_Width, _Y8ImageBuffer, clippedBallRect);

            // TODO: These calculations do only work for FullHD and are based on one specific real world scenario observation

            // Finder quality is up to 50 for perfect balls (it's the pixel count of inliers)
            int circleFinderQualityZeroToFifty = Math.Min(circle.PointCount, 50);

            // PixelCount is up to 1200 for perfect balls, so devide it by 24 so that it is in range 0..50
            int pixelCountQualityZeroToFifty = Math.Min(pixelCount / 24, 50);

            // Overall quality should be 0..1
            double overallQuality = (circleFinderQualityZeroToFifty + pixelCountQualityZeroToFifty) / 100.0;

            ObservedBall ball = new(new Point(x, y), overallQuality);
            balls.Add(ball);
        }

        return balls;
    }
}
