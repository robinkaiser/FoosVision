// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Game.Entities;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.UseCases.Game.StopGame;

public class StopGameInteractor : IStopGameInputPort
{
    private readonly IGameSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;

    public StopGameInteractor(
        IGameSessionStore sessionStore,
        IFrameSource frameSource)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
    }

    public async Task Handle(StopGameRequest request, IStopGameOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out GameSession session))
        {
            await output.ReportStopFailed("No active session.");
            return;
        }

        var sessionId = session.Id;

        await _FrameSource.Stop(ct);

        _SessionStore.Clear();

        await output.ReportStopped(new StopGameResponse(sessionId));
    }
}
