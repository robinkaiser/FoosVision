// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Setup.Entities;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Setup.Ports;

namespace FoosVision.UseCases.Setup.StartSetup;

public class StartSetupInteractor : IStartSetupInputPort
{
    private readonly ISetupSessionStore _SessionStore;
    private readonly IFrameSource _FrameSource;
    private readonly SessionGate _SessionGate;

    public StartSetupInteractor(
        ISetupSessionStore sessionStore,
        IFrameSource frameSource,
        SessionGate sessionGate)
    {
        _SessionStore = sessionStore;
        _FrameSource = frameSource;
        _SessionGate = sessionGate;
    }

    public async Task Handle(StartSetupRequest request, IStartSetupOutputPort output, CancellationToken ct)
    {
        if (_SessionStore.HasActive)
        {
            await output.ReportStartFailed("A setup session is already active.");
            return;
        }

        using SessionGate.Reservation? reservation = _SessionGate.TryReserve();
        if (reservation is null)
        {
            await output.ReportStartFailed("Another recorder session is active or starting.");
            return;
        }

        bool started = false;
        StartSetupResponse response;
        try
        {
            var result = await _FrameSource.Configure(ct);

            if (result == FrameSourceResult.Failure)
            {
                await output.ReportStartFailed("Configure frame source failed.");
                return;
            }

            SetupSession session = new(reservation.SessionId);

            _SessionStore.SaveActive(session);

            result = await _FrameSource.Start(ct);

            if (result == FrameSourceResult.Failure)
            {
                _SessionStore.Clear();
                await output.ReportStartFailed("Start frame source failed.");
                return;
            }

            response = new(session.Id);
            await output.ReportStarted(response);
            started = true;
            reservation.Commit();
        }
        finally
        {
            if (!started)
            {
                _SessionStore.Clear();
                await _FrameSource.Stop(CancellationToken.None);
            }
        }
    }
}
