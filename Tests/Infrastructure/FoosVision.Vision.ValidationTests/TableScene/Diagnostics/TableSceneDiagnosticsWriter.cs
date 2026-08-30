// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class TableSceneDiagnosticsWriter
{
    public static void WriteFieldDetectionFailed(TableSceneTestContext context)
    {
        File.WriteAllLines(
            context.OutputPaths.ColoredObjectIntervalSummary,
            [
                context.RelativeName,
                "Field detection failed. TableScene colored object interval detection was not run.",
            ]);
    }

    public static void WriteColoredObjectIntervals(
        TableSceneTestContext context,
        PlayingField field,
        ColoredRodObjectIntervalDetection detection)
    {
        ColoredObjectIntervalSummaryWriter.Write(detection, context.OutputPaths.ColoredObjectIntervalSummary);
        ColoredObjectIntervalOverlayWriter.Write(context.Image, field, detection, context.OutputPaths.ColoredObjectIntervalOverlay);
        EdgeScoreProfileWriter.Write(detection, context.OutputPaths.EdgeScoreProfile);
    }

    public static void WriteColorModels(
        TableSceneTestContext context,
        ColoredPlayerColorCalibration calibration)
    {
        ColorModelSummaryWriter.Write(calibration, context.OutputPaths.ColorModelSummary);
        ColorModelMosaicWriter.Write(calibration, context.OutputPaths.ColorModelMosaic);
    }

    public static void WriteObjectMask(
        TableSceneTestContext context,
        ColoredPlayerMaskDetection detection)
    {
        ObjectMaskOverlayWriter.Write(context.Image, detection, context.OutputPaths.ObjectMaskOverlay);
    }

    public static void WriteBlackObjectIntervals(
        TableSceneTestContext context,
        PlayingField field,
        BlackRodObjectIntervalDetection detection)
    {
        BlackObjectIntervalSummaryWriter.Write(detection, context.OutputPaths.BlackObjectIntervalSummary);
        BlackSideBandHistogramWriter.Write(detection, context.OutputPaths.BlackSideBandHistogram);
        BlackSideBandProfileWriter.Write(detection, context.OutputPaths.BlackSideBandProfile);
        BlackObjectIntervalOverlayWriter.Write(context.Image, field, detection, context.OutputPaths.BlackObjectIntervalOverlay);
    }

    public static void WriteBlackObjectMasks(
        TableSceneTestContext context,
        BlackRodObjectMaskDetection detection)
    {
        BlackObjectMaskOverlayWriter.Write(context.Image, detection, context.OutputPaths.BlackObjectMaskOverlay);
    }
}
