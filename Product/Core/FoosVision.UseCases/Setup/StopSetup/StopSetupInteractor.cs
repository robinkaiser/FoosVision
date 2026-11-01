// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Setup.Entities;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Setup.Ports;

namespace FoosVision.UseCases.Setup.StopSetup;

public class StopSetupInteractor : IStopSetupInputPort
{
    private readonly ISetupSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;
    private readonly SessionGate _SessionGate;

    public StopSetupInteractor(
        ISetupSessionStore sessionStore,
        IFrameSource frameSource,
        SessionGate sessionGate)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
        _SessionGate = sessionGate;
    }

    public async Task Handle(StopSetupRequest request, IStopSetupOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out SetupSession session))
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
                await output.ReportStopped(new StopSetupResponse(sessionId));
            }
            finally
            {
                _SessionGate.Release(sessionId);
            }
        }
    }
}
