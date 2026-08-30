// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Replay.StopReplay;

public record StopReplayRequest;

public interface IStopReplayInputPort
{
    Task Handle(StopReplayRequest request, IStopReplayOutputPort output, CancellationToken ct);
}
