// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Setup.Entities;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Setup.Ports;

namespace FoosVision.UseCases.Setup.StartSetup;

public class StartSetupInteractor : IStartSetupInputPort
{
    private readonly ISetupSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;

    public StartSetupInteractor(
        ISetupSessionStore sessionStore,
        IFrameSource frameSource)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
    }

    public async Task Handle(StartSetupRequest request, IStartSetupOutputPort output, CancellationToken ct)
    {
        if (_SessionStore.HasActive)
        {
            await output.ReportStartFailed("A setup session is already active.");
            return;
        }

        var result = await _FrameSource.Configure(ct);

        if (result == FrameSourceResult.Failure)
        {
            await output.ReportStartFailed("Configure frame source failed.");
            return;
        }

        Guid guid = Guid.NewGuid();
        SetupSession session = new(guid);

        _SessionStore.SaveActive(session);

        result = await _FrameSource.Start(ct);

        if (result == FrameSourceResult.Failure)
        {
            _SessionStore.Clear();
            await output.ReportStartFailed("Start frame source failed.");
            return;
        }

        StartSetupResponse response = new(session.Id);

        await output.ReportStarted(response);
    }
}
