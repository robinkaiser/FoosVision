// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.Replay.CompleteReplayLoop;

public class CompleteReplayLoopInteractor : ICompleteReplayLoopInputPort
{
    private readonly IReplaySessionStore _SessionStore;

    public CompleteReplayLoopInteractor(IReplaySessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(CompleteReplayLoopRequest request, ICompleteReplayLoopOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out ReplaySession session))
        {
            await output.ReportSkipped("No active replay.");
            return;
        }

        session.CompleteLoop();
    }
}
