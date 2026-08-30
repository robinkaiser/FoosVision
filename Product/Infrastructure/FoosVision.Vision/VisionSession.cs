// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Vision;
using FoosVision.Vision.BallFinding;
using FoosVision.Vision.TableConfig;
using FoosVision.Vision.TableScene;

namespace FoosVision.Vision;

public class VisionSession :
    ITableConfigFinder,
    ITableSceneUpdater,
    IBallFinder,
    IEncodedVisionContextProvider,
    IEncodedVisionContextConsumer,
    IEncodedBallDetectionMaskProvider,
    IEncodedBallDetectionMaskDecoder
{
    private const int _MaxBallFinderCircleParts = 50;

    private readonly FieldDetector _FieldDetector;
    private readonly TableSceneModel _TableScene;
    private readonly TableSceneCalibrator _TableSceneCalibrator;
    private readonly TableSceneUpdater _TableSceneUpdater;
    private readonly BallFinder _BallFinder;
    private readonly int _Width;
    private readonly int _Height;
    private readonly byte[] _EncodedBallDetectionMaskBuffer;

    private bool _HasTableSceneCalibration;
    private PlayerColors _PlayerColors;

    public VisionSession(VisionOptions options)
    {
        var layout = options.Layout;
        var format = layout.Format;
        _Width = layout.Width;
        _Height = layout.Height;

        if (format != VisionPixelFormat.RGBA8888 ||
            _Width != layout.Stride)
        {
            throw new NotImplementedException();
        }

        _FieldDetector = new(_Width, _Height);
        _TableScene = new(_Width, _Height);
        _TableSceneCalibrator = new(_Width, _Height);
        _TableSceneUpdater = new(_Width, _Height, _TableScene);
        _BallFinder = new(_Width, _Height, _TableSceneUpdater, _MaxBallFinderCircleParts);
        _EncodedBallDetectionMaskBuffer = new byte[BallDetectionMaskRleCodec.GetMaxEncodedLength(_Width * _Height)];
    }

    Option<TableConfiguration> ITableConfigFinder.Detect(byte[] frameBuffer)
    {
        var field = _FieldDetector.Detect(frameBuffer);

        if (field.IsNone) return Option<TableConfiguration>.None();

        _TableSceneUpdater.ApplyField(field.Value);

        if (!_HasTableSceneCalibration)
        {   // One-time calibration for now
            var calibration = _TableSceneCalibrator.Calibrate(frameBuffer, field.Value);

            if (!TableScenePlayerColorMapper.TryCreatePlayerColors(calibration, out _PlayerColors))
            {
                return Option<TableConfiguration>.None();
            }

            _TableSceneUpdater.ApplyCalibration(calibration);
            _HasTableSceneCalibration = true;
        }

        return new TableConfiguration(field.Value, _PlayerColors, BallColor.White);
    }

    void ITableSceneUpdater.Update(
        byte[] frameBufferRGBA8888,
        TableConfiguration tableConfig,
        Option<Point> ballPosition)
    {
        _TableSceneUpdater.Update(frameBufferRGBA8888, tableConfig, ballPosition);
    }

    IReadOnlyList<ObservedBall> IBallFinder.Detect(
        byte[] frameBufferRGBA8888,
        TableConfiguration tableConfig)
    {
        return _BallFinder.Detect(frameBufferRGBA8888, tableConfig);
    }

    IReadOnlyList<ObservedBall> IBallFinder.Detect(
        byte[] frameBufferRGBA8888,
        TableConfiguration tableConfig,
        Rectangle regionOfInterest)
    {
        return _BallFinder.Detect(frameBufferRGBA8888, tableConfig, regionOfInterest);
    }

    IReadOnlyList<ObservedBall> IBallFinder.DetectYuv420(
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
        return _BallFinder.DetectYuv420(
            bufferY,
            bufferU,
            bufferV,
            width,
            height,
            yRowStride,
            yPixelStride,
            uRowStride,
            uPixelStride,
            vRowStride,
            vPixelStride,
            tableConfig,
            regionOfInterest);
    }

    bool IEncodedVisionContextProvider.TryGetEncodedVisionContext(out EncodedVisionContext context)
    {
        return _TableSceneUpdater.TryGetEncodedVisionContext(out context);
    }

    bool IEncodedVisionContextConsumer.TryApplyEncodedVisionContext(EncodedVisionContext context)
    {
        return _TableSceneUpdater.TryApplyEncodedVisionContext(context);
    }

    void IEncodedBallDetectionMaskProvider.GetEncodedBallDetectionMask(out EncodedBallDetectionMask mask)
    {
        var rawMask = _BallFinder.BallDetectionMask;
        int encodedLength = BallDetectionMaskRleCodec.Encode(_Width, _Height, rawMask, _EncodedBallDetectionMaskBuffer);

        mask = new EncodedBallDetectionMask(_EncodedBallDetectionMaskBuffer, encodedLength, _Width, _Height);
    }

    void IEncodedBallDetectionMaskDecoder.DecodeBallDetectionMask(EncodedBallDetectionMask mask, byte[] outputGray8)
    {
        BallDetectionMaskRleCodec.DecodeToGray8(
            mask.Width,
            mask.Height,
            mask.Buffer,
            mask.Length,
            outputGray8);
    }
}
