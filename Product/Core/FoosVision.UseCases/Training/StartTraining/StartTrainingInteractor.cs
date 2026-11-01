// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.Services;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.Training.StartTraining;

public class StartTrainingInteractor : IStartTrainingInputPort
{
    private readonly ITrainingSessionStore _SessionStore;
    private readonly ISettingsStore _Settings;
    private readonly IFrameSource _FrameSource;
    private readonly SessionGate _SessionGate;
    private readonly ITrainingOptionsProvider _Options;
    private readonly ITrainingRandom _Random;

    public StartTrainingInteractor(
        ITrainingSessionStore sessionStore,
        ISettingsStore settings,
        IFrameSource frameSource,
        ITrainingOptionsProvider options,
        ITrainingRandom random,
        SessionGate sessionGate)
    {
        _SessionStore = sessionStore;
        _Settings = settings;
        _FrameSource = frameSource;
        _SessionGate = sessionGate;
        _Options = options;
        _Random = random;
    }

    public async Task Handle(StartTrainingRequest request, IStartTrainingOutputPort output, CancellationToken ct)
    {
        if (_SessionStore.HasActive)
        {
            await output.ReportStartFailed("A training session is already active.");
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

        TrainingSession? session = null;
        bool started = false;
        StartTrainingResponse response;
        try
        {
            TrainingOptions options = _Options.LoadTrainingOptions();
            FrameSourceResult result = await _FrameSource.Configure(ct);

            if (result == FrameSourceResult.Failure)
            {
                await output.ReportStartFailed("Configure frame source failed.");
                return;
            }

            BallTracker ballTracker = new(BallTrackerParams.Default, tableConfig);
            PossessionCalculator possession = new(tableConfig);
            LiveTracker liveTracker = new(LiveTrackerParams.Default, ballTracker, possession);
            session = new(reservation.SessionId, liveTracker, tableConfig, options, _Random);

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
                session?.Stop();
                _SessionStore.Clear();
                await _FrameSource.Stop(CancellationToken.None);
            }
        }
    }
}
