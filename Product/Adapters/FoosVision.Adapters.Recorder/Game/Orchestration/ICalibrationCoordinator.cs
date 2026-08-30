// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Adapters.Recorder.Game.Orchestration;

public interface ICalibrationCoordinator
{
    Task RequestTableUpdate(Frame frame);

    Task RequestTableSceneUpdate(Frame frame, Option<Point> ballPosition);
}
