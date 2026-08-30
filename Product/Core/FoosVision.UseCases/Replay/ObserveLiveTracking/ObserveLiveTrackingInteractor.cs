// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.Replay.ObserveLiveTracking;

public class ObserveLiveTrackingInteractor : IObserveLiveTrackingInputPort
{
    private readonly IReplaySessionStore _SessionStore;

    public ObserveLiveTrackingInteractor(IReplaySessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(ObserveLiveTrackingRequest request, IObserveLiveTrackingOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out ReplaySession session))
        {
            return;
        }

        if (!session.CanReturnToLive(request.BallPosition))
        {
            return;
        }

        ReplayId replayId = session.CurrentReplayId.Value;

        _SessionStore.Clear();

        await output.ReportReturnToLive(new ReturnToLiveResponse(replayId));
    }
}
