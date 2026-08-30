// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.UseCases.Replay.ObserveLiveTracking;

public record ObserveLiveTrackingRequest(Point? BallPosition);

public interface IObserveLiveTrackingInputPort
{
    Task Handle(ObserveLiveTrackingRequest request, IObserveLiveTrackingOutputPort output, CancellationToken ct);
}
