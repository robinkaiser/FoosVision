// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.UseCases.Game.Ports;

public interface IFrameVisionOps
{
    IReadOnlyList<ObservedBall> DetectBalls(TableConfiguration tableConfig);
}
