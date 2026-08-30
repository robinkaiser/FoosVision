// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Game.Entities;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.GameTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.UseCases.Game.StartGame;

public class StartGameInteractor : IStartGameInputPort
{
    private readonly IGameSessionStore _SessionStore;
    private readonly ISettingsStore _Settings;
    private readonly IFrameSource _FrameSource;

    public StartGameInteractor(
        IGameSessionStore sessionStore,
        ISettingsStore settings,
        IFrameSource frameSource)
    {
        _SessionStore = sessionStore;
        _Settings = settings;
        _FrameSource = frameSource;
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

        var result = await _FrameSource.Configure(ct);

        if (result == FrameSourceResult.Failure)
        {
            await output.ReportStartFailed("Configure frame source failed.");
            return;
        }

        Guid guid = Guid.NewGuid();
        PossessionCalculator calculator = new(tableConfig);
        IReplayDecider replayDecider = ReplayDeciderFactory.CreateDefault(tableConfig);
        BallTracker ballTracker = new(BallTrackerParams.Default, tableConfig);
        GameTracker gameTracker = new(GameTrackerParams.Default, ballTracker, calculator, replayDecider);
        GameSession session = new(guid, gameTracker, tableConfig);

        _SessionStore.SaveActive(session);

        result = await _FrameSource.Start(ct);

        if (result == FrameSourceResult.Failure)
        {
            _SessionStore.Clear();
            await output.ReportStartFailed("Start frame source failed.");
            return;
        }

        StartGameResponse response = new(session.Id);

        await output.ReportStarted(response);
    }
}
