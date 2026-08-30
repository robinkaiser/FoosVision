// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Adapters.Recorder.Installation.Orchestration;

public interface ICalibrationCoordinator
{
    Task RequestUpdate(Frame frame);
}
