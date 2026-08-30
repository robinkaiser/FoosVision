// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.TableScene;

public class TableSceneCalibrator
{
    private static readonly Source _Log = new("Vision.TableSceneCalibrator");

    private readonly ColoredRodObjectIntervalDetector _ColoredObjectIntervalDetector;
    private readonly ColoredPlayerColorModelCalibrator _ColoredPlayerColorModelCalibrator;
    private readonly ColoredPlayerMaskDetector _ColoredPlayerMaskDetector;
    private readonly BlackRodObjectIntervalDetector _BlackObjectIntervalDetector;
    private readonly BlackRodObjectMaskDetector _BlackObjectMaskDetector;
    private readonly RodObjectMask[] _IgnoredMasks;

    public TableSceneCalibrator(int width, int height)
    {
        _ColoredObjectIntervalDetector = new(width, height);
        _ColoredPlayerColorModelCalibrator = new(sampleCapacity: Math.Max(width, height) * 8);
        _ColoredPlayerMaskDetector = new(width, height);
        _BlackObjectIntervalDetector = new(width, height);
        _BlackObjectMaskDetector = new(width, height);
        _IgnoredMasks = new RodObjectMask[8];
    }

    public TableSceneCalibration Calibrate(byte[] frameBufferRGBA8888, PlayingField field)
    {
        var coloredObjectIntervals = _ColoredObjectIntervalDetector.Detect(frameBufferRGBA8888, field);

        var colorCalibration = _ColoredPlayerColorModelCalibrator.Calibrate(coloredObjectIntervals);

        var coloredPlayerMasks = _ColoredPlayerMaskDetector.Detect(frameBufferRGBA8888, field, colorCalibration);

        CreateIgnoredMasks(coloredPlayerMasks);

        bool hasTwoColoredTeamModels = colorCalibration.TeamA.HasColorModel && colorCalibration.TeamB.HasColorModel;
        var blackObjectIntervals = _BlackObjectIntervalDetector.Detect(
            frameBufferRGBA8888,
            field,
            _IgnoredMasks,
            hasTwoColoredTeamModels);

        var blackObjectMasks = _BlackObjectMaskDetector.Detect(frameBufferRGBA8888, field, blackObjectIntervals);

        LogCalibration(colorCalibration, blackObjectIntervals.Rule);

        return new(
            coloredObjectIntervals,
            colorCalibration,
            coloredPlayerMasks,
            blackObjectIntervals,
            blackObjectMasks);
    }

    private void CreateIgnoredMasks(ColoredPlayerMaskDetection coloredPlayerMasks)
    {
        for (int i = 0; i < _IgnoredMasks.Length; i++)
        {
            var rod = coloredPlayerMasks.Rods[i];
            _IgnoredMasks[i] = new(rod.BarType, rod.Rectangles);
        }
    }

    private static void LogCalibration(
        ColoredPlayerColorCalibration colorCalibration,
        BlackObjectRule blackObjectRule)
    {
        _Log.Information(
            "TableScene calibration completed. " +
            "TeamA={TeamA} TeamAIntervals={TeamAIntervals} TeamAChromaticSamples={TeamAChromaticSamples} TeamAHasColorModel={TeamAHasColorModel} TeamACenterCb={TeamACenterCb} TeamACenterCr={TeamACenterCr} TeamARadius={TeamARadius} TeamAMinimumChromaticDistance={TeamAMinimumChromaticDistance} TeamAModelSamples={TeamAModelSamples} " +
            "TeamB={TeamB} TeamBIntervals={TeamBIntervals} TeamBChromaticSamples={TeamBChromaticSamples} TeamBHasColorModel={TeamBHasColorModel} TeamBCenterCb={TeamBCenterCb} TeamBCenterCr={TeamBCenterCr} TeamBRadius={TeamBRadius} TeamBMinimumChromaticDistance={TeamBMinimumChromaticDistance} TeamBModelSamples={TeamBModelSamples} " +
            "BlackObjectMaximumY={BlackObjectMaximumY}",
            colorCalibration.TeamA.Team,
            colorCalibration.TeamA.IntervalCount,
            colorCalibration.TeamA.ChromaticSampleCount,
            colorCalibration.TeamA.HasColorModel,
            colorCalibration.TeamA.ColorModel?.CenterCb,
            colorCalibration.TeamA.ColorModel?.CenterCr,
            colorCalibration.TeamA.ColorModel?.Radius,
            colorCalibration.TeamA.ColorModel?.MinimumChromaticDistance,
            colorCalibration.TeamA.ColorModel?.SampleCount,
            colorCalibration.TeamB.Team,
            colorCalibration.TeamB.IntervalCount,
            colorCalibration.TeamB.ChromaticSampleCount,
            colorCalibration.TeamB.HasColorModel,
            colorCalibration.TeamB.ColorModel?.CenterCb,
            colorCalibration.TeamB.ColorModel?.CenterCr,
            colorCalibration.TeamB.ColorModel?.Radius,
            colorCalibration.TeamB.ColorModel?.MinimumChromaticDistance,
            colorCalibration.TeamB.ColorModel?.SampleCount,
            blackObjectRule.MaximumObjectY);
    }
}
