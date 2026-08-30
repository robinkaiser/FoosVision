// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.UseCases.Calibration.Ports;

namespace FoosVision.UseCases.Calibration.UpdateTableScene;

public record UpdateTableSceneRequest(
    Frame Frame,
    Option<Point> BallPosition,
    ITableSceneUpdateVisionOps Vision);

public interface IUpdateTableSceneInputPort
{
    Task Handle(UpdateTableSceneRequest request, IUpdateTableSceneOutputPort output, CancellationToken ct);
}
