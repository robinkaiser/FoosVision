// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;

namespace FoosVision.Vision.TableScene;

public record TableSceneCalibration(
    ColoredRodObjectIntervalDetection ColoredObjectIntervals,
    ColoredPlayerColorCalibration ColoredPlayerColorCalibration,
    ColoredPlayerMaskDetection ColoredPlayerMasks,
    BlackRodObjectIntervalDetection BlackObjectIntervals,
    BlackRodObjectMaskDetection BlackObjectMasks);
