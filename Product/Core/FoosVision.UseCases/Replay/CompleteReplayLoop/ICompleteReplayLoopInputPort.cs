// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Replay.CompleteReplayLoop;

public record CompleteReplayLoopRequest;

public interface ICompleteReplayLoopInputPort
{
    Task Handle(CompleteReplayLoopRequest request, ICompleteReplayLoopOutputPort output, CancellationToken ct);
}
