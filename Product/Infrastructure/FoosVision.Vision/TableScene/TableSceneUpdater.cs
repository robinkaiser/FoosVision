// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Vision;
using FoosVision.Vision.Common;
using FoosVision.Vision.TableScene.Processing;
using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;

namespace FoosVision.Vision.TableScene;

public class TableSceneUpdater : IBallDetectionContextProvider
{
    private const int _BallRadiusFullHDFullTable = 20;
    private const double _BallFinderPlayerColorExclusionRadiusScale = 2.0;

    private readonly Lock _BackgroundLock = new();
    private readonly int _Width;
    private readonly int _Height;
    private readonly TableSceneModel _TableScene;
    private readonly byte[] _ColorResponseImage;
    private readonly VisionContextManager _VisionContextManager;
    private readonly byte[] _Rgba8888TempImage;
    private readonly ColoredPlayerMaskDetector _ColoredPlayerMaskDetector;
    private readonly BlackRodObjectMaskDetector _BlackObjectMaskDetector;
    private readonly Rectangle[] _ColoredPlayerRectangles;
    private readonly Rectangle[] _BlackObjectRectangles;
    private readonly RodColoredPlayerMaskRange[] _ColoredPlayerRodRanges;
    private readonly RodBlackObjectMaskRange[] _BlackObjectRodRanges;

    private TableSceneCalibration? _Calibration;
    private int _ColoredPlayerRectanglesCount;
    private int _BlackObjectRectanglesCount;

    public TableSceneUpdater(int width, int height, TableSceneModel tableScene)
    {
        _Width = width;
        _Height = height;
        _TableScene = tableScene;

        int size = width * height;
        _ColorResponseImage = new byte[size * 4];
        BallColorThresholding.InitializeColorResponse(_ColorResponseImage, BallColor.White);
        _VisionContextManager = new VisionContextManager(width, height, _ColorResponseImage);
        _Rgba8888TempImage = new byte[size * 4];
        _ColoredPlayerMaskDetector = new(width, height);
        _BlackObjectMaskDetector = new(width, height);

        _ColoredPlayerRectangles = new Rectangle[1000];
        _BlackObjectRectangles = new Rectangle[1000];
        _ColoredPlayerRodRanges = new RodColoredPlayerMaskRange[8];
        _BlackObjectRodRanges = new RodBlackObjectMaskRange[8];
    }

    public byte[] Rgba8888TempImage => _Rgba8888TempImage;

    public Rectangle[] ColoredPlayerRectangles => _ColoredPlayerRectangles;

    public Rectangle[] BlackObjectRectangles => _BlackObjectRectangles;

    public byte[] ColorResponse32bpp => _ColorResponseImage;

    public PlayerColorExclusionContext PlayerColorExclusion => _VisionContextManager.PlayerColorExclusion;

    public bool TryGetEncodedVisionContext(out EncodedVisionContext context)
        => _VisionContextManager.TryGetEncodedVisionContext(out context);

    public bool TryApplyEncodedVisionContext(EncodedVisionContext context)
        => _VisionContextManager.TryApplyEncodedVisionContext(context);

    public void ApplyCalibration(TableSceneCalibration calibration)
    {
        _Calibration = calibration;
        _VisionContextManager.PlayerColorExclusion = CreatePlayerColorExclusion(calibration.ColoredPlayerColorCalibration);
    }

    public void ApplyField(PlayingField field)
    {
        lock (_BackgroundLock)
        {   // Working on the background while frame processing is running should be okay,
            // since BallFinder only uses color response image and not the state, min or max images.
            BackgroundAdaption.ResetIgnoredPixels(_Width, _Height, _TableScene.StateImage, _TableScene.MinImage, _TableScene.MaxImage);

            IgnoreBar(field.Bars.A2);
            IgnoreBar(field.Bars.B3);
            IgnoreBar(field.Bars.A5);
            IgnoreBar(field.Bars.B5);
            IgnoreBar(field.Bars.A3);
            IgnoreBar(field.Bars.B2);

            for (int i = 0; i < field.Occlusions.Count; i++)
            {
                BackgroundMasking.IgnoreInsideTrapeziumMask(_Width, _Height, _TableScene.StateImage, field.Occlusions[i]);
            }

            BackgroundMasking.IgnoreOutsideTrapeziumMask(_Width, _Height, _TableScene.StateImage, field.Boundary);
        }
    }

    public void Update(byte[] frameBufferRGBA8888, TableConfiguration tableConfig, Option<Point> ballPosition)
    {
        lock (_BackgroundLock)
        {
            TableSceneCalibration calibration = _Calibration ??
                throw new InvalidOperationException("TableScene calibration must be applied before updating the table scene.");

            int imageSize = _Width * _Height * 4;

            // Make a copy since frameBuffer must not be altered
            Array.Copy(frameBufferRGBA8888, _Rgba8888TempImage, imageSize);

            if (ballPosition.HasValue)
            {   // Ball position will be ignored for update (add 50% to ball size just in case)
                int radius = Convert.ToInt32(_BallRadiusFullHDFullTable * 1.5);
                int size = (radius * 2) + 1;
                Point p = ballPosition.Value;
                Rectangle ballRect = new((int)(p.X - radius), (int)(p.Y - radius), size, size);
                BackgroundMasking.IgnoreInsideRectangleRgba(_Width, _Height, _Rgba8888TempImage, ballRect);
            }

            _ColoredPlayerRectanglesCount = _ColoredPlayerMaskDetector.DetectRectangles(
                frameBufferRGBA8888,
                tableConfig.Field,
                calibration.ColoredPlayerColorCalibration,
                _ColoredPlayerRectangles,
                _ColoredPlayerRodRanges);

            for (int i = 0; i < _ColoredPlayerRectanglesCount; i++)
            {
                BackgroundMasking.IgnoreInsideRectangleRgba(_Width, _Height, _Rgba8888TempImage, _ColoredPlayerRectangles[i]);
            }

            _BlackObjectRectanglesCount = _BlackObjectMaskDetector.DetectRectangles(
                frameBufferRGBA8888,
                tableConfig.Field,
                calibration.BlackObjectIntervals,
                _BlackObjectRectangles,
                _BlackObjectRodRanges);

            for (int i = 0; i < _BlackObjectRectanglesCount; i++)
            {
                BackgroundMasking.IgnoreInsideRectangleRgba(_Width, _Height, _Rgba8888TempImage, _BlackObjectRectangles[i]);
            }

            BackgroundAdaption.UpdateModelFromRgba(_Width, _Height, _Rgba8888TempImage,
                _TableScene.StateImage, _TableScene.MinImage, _TableScene.MaxImage);

            BallColorThresholding.ComputeBallColorThresholds(_Width, _Height,
                _TableScene.StateImage, _TableScene.MinImage, _TableScene.MaxImage,
                _ColorResponseImage, IBallDetectionContextProvider.IgnoredPixel, tableConfig.Ball);
        }
    }

    private static PlayerColorExclusionContext CreatePlayerColorExclusion(ColoredPlayerColorCalibration calibration)
    {
        bool hasTeamA = TryCreateBallDetectionColorModel(calibration.TeamA.ColorModel, out BallDetectionColorModel teamA);
        bool hasTeamB = TryCreateBallDetectionColorModel(calibration.TeamB.ColorModel, out BallDetectionColorModel teamB);

        return new(hasTeamA, teamA, hasTeamB, teamB);
    }

    private static bool TryCreateBallDetectionColorModel(
        ChromaticColorModel? model,
        out BallDetectionColorModel ballDetectionColorModel)
    {
        if (model is null)
        {
            ballDetectionColorModel = default;
            return false;
        }

        ballDetectionColorModel = new(
            model.CenterCb,
            model.CenterCr,
            Convert.ToInt32(Math.Ceiling(
                model.RadiusSquared *
                _BallFinderPlayerColorExclusionRadiusScale *
                _BallFinderPlayerColorExclusionRadiusScale)),
            model.MinimumChromaticDistance * model.MinimumChromaticDistance);
        return true;
    }

    private void IgnoreBar(Bar bar)
    {
        VerticalChannel barChannel = new(bar.Left.P0.X, bar.Left.P1.X, bar.Right.P0.X, bar.Right.P1.X);
        BackgroundMasking.IgnoreInsideVerticalChannelMask(_Width, _Height, _TableScene.StateImage, barChannel);
    }
}
