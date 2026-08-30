// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Installation.Entities;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Installation.Ports;

namespace FoosVision.UseCases.Installation.StopInstall;

public class StopInstallInteractor : IStopInstallInputPort
{
    private readonly IInstallSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;

    public StopInstallInteractor(
        IInstallSessionStore sessionStore,
        IFrameSource frameSource)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
    }

    public async Task Handle(StopInstallRequest request, IStopInstallOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out InstallSession session))
        {
            await output.ReportStopFailed("No active session.");
            return;
        }

        var sessionId = session.Id;

        await _FrameSource.Stop(ct);

        _SessionStore.Clear();

        await output.ReportStopped(new StopInstallResponse(sessionId));
    }
}
