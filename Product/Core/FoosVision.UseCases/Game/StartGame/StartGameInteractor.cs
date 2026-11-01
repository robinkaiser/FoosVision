// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Game.Entities;
using FoosVision.Domain.Game.Services.GameTracking;
using FoosVision.Domain.Game.Services.ReplayDecision;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.UseCases.Game.StartGame;

public class StartGameInteractor : IStartGameInputPort
{
    private readonly IGameSessionStore _SessionStore;
    private readonly ISettingsStore _Settings;
    private readonly IFrameSource _FrameSource;
    private readonly SessionGate _SessionGate;

    public StartGameInteractor(
        IGameSessionStore sessionStore,
        ISettingsStore settings,
        IFrameSource frameSource,
        SessionGate sessionGate)
    {
        _SessionStore = sessionStore;
        _Settings = settings;
        _FrameSource = frameSource;
        _SessionGate = sessionGate;
    }

    public async Task Handle(StartGameRequest request, IStartGameOutputPort output, CancellationToken ct)
    {
        if (_SessionStore.HasActive)
        {
            await output.ReportStartFailed("A game session is already active.");
            return;
        }

        if (!_Settings.LoadTableConfig().TryGetValue(out TableConfiguration tableConfig))
        {
            await output.ReportStartFailed("No table configuration available.");
            return;
        }

        using SessionGate.Reservation? reservation = _SessionGate.TryReserve();
        if (reservation is null)
        {
            await output.ReportStartFailed("Another recorder session is active or starting.");
            return;
        }

        bool started = false;
        StartGameResponse response;
        try
        {
            var result = await _FrameSource.Configure(ct);

            if (result == FrameSourceResult.Failure)
            {
                await output.ReportStartFailed("Configure frame source failed.");
                return;
            }

            PossessionCalculator calculator = new(tableConfig);
            IReplayDecider replayDecider = ReplayDeciderFactory.CreateDefault(tableConfig);
            BallTracker ballTracker = new(BallTrackerParams.Default, tableConfig);
            LiveTracker liveTracker = new(LiveTrackerParams.Default, ballTracker, calculator);
            GameTracker gameTracker = new(liveTracker, replayDecider);
            GameSession session = new(reservation.SessionId, gameTracker, tableConfig);

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
