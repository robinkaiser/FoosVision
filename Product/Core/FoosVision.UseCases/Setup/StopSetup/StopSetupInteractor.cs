// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Setup.Entities;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Setup.Ports;

namespace FoosVision.UseCases.Setup.StopSetup;

public class StopSetupInteractor : IStopSetupInputPort
{
    private readonly ISetupSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;

    public StopSetupInteractor(
        ISetupSessionStore sessionStore,
        IFrameSource frameSource)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
    }

    public async Task Handle(StopSetupRequest request, IStopSetupOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out SetupSession session))
        {
            await output.ReportStopFailed("No active session.");
            return;
        }

        var sessionId = session.Id;

        await _FrameSource.Stop(ct);

        _SessionStore.Clear();

        await output.ReportStopped(new StopSetupResponse(sessionId));
    }
}
