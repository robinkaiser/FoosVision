// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.UseCases.Replay.Ports;

public interface IReplayVisionOps
{
    IReadOnlyList<ObservedBall> DetectBalls(TableConfiguration tableConfiguration, Rectangle regionOfInterest);
}
