// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Game.Entities;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.UseCases.Game.StopGame;

public class StopGameInteractor : IStopGameInputPort
{
    private readonly IGameSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;
    private readonly SessionGate _SessionGate;

    public StopGameInteractor(
        IGameSessionStore sessionStore,
        IFrameSource frameSource,
        SessionGate sessionGate)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
        _SessionGate = sessionGate;
    }

    public async Task Handle(StopGameRequest request, IStopGameOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out GameSession session))
        {
            await output.ReportStopFailed("No active session.");
            return;
        }

        var sessionId = session.Id;

        try
        {
            await _FrameSource.Stop(ct);
        }
        finally
        {
            _SessionStore.Clear();

            try
            {
                await output.ReportStopped(new StopGameResponse(sessionId));
            }
            finally
            {
                _SessionGate.Release(sessionId);
            }
        }
    }
}
