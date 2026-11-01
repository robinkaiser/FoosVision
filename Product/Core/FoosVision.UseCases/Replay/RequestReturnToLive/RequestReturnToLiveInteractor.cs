// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.Replay.RequestReturnToLive;

public class RequestReturnToLiveInteractor : IRequestReturnToLiveInputPort
{
    private readonly IReplaySessionStore _SessionStore;

    public RequestReturnToLiveInteractor(IReplaySessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(RequestReturnToLiveRequest request, IRequestReturnToLiveOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out ReplaySession session))
        {
            return;
        }

        session.RequestReturnToLive(request.ReplayId);
        if (!session.CanReturnToLive)
        {
            return;
        }

        ReplayId replayId = session.CurrentReplayId.Value;

        _SessionStore.Clear();

        await output.ReportReturnToLive(new ReturnToLiveResponse(replayId));
    }
}
