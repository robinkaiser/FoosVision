// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Installation.Entities;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Installation.Ports;

namespace FoosVision.UseCases.Installation.StartInstall;

public class StartInstallInteractor : IStartInstallInputPort
{
    private readonly IInstallSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;

    public StartInstallInteractor(
        IInstallSessionStore sessionStore,
        IFrameSource frameSource)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
    }

    public async Task Handle(StartInstallRequest request, IStartInstallOutputPort output, CancellationToken ct)
    {
        if (_SessionStore.HasActive)
        {
            await output.ReportStartFailed("A install session is already active.");
            return;
        }

        var result = await _FrameSource.Configure(ct);

        if (result == FrameSourceResult.Failure)
        {
            await output.ReportStartFailed("Configure frame source failed.");
            return;
        }

        Guid guid = Guid.NewGuid();
        InstallSession session = new(guid);

        _SessionStore.SaveActive(session);

        result = await _FrameSource.Start(ct);

        if (result == FrameSourceResult.Failure)
        {
            _SessionStore.Clear();
            await output.ReportStartFailed("Start frame source failed.");
            return;
        }

        StartInstallResponse response = new(session.Id);

        await output.ReportStarted(response);
    }
}
