// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.Replay.StopReplay;

public class StopReplayInteractor : IStopReplayInputPort
{
    private readonly IReplaySessionStore _SessionStore;

    public StopReplayInteractor(IReplaySessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(StopReplayRequest request, IStopReplayOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out ReplaySession session))
        {
            await output.ReportStopFailed("No active replay.");
            return;
        }

        ReplayId replayId = session.CurrentReplayId.Value;

        _SessionStore.Clear();

        await output.ReportStopped(new ReplayStoppedResponse(replayId));
    }
}
