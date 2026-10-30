// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.Training.StartTraining;

public class StartTrainingInteractor : IStartTrainingInputPort
{
    private readonly ITrainingSessionStore _Sessions;
    private readonly ISettingsStore _Settings;
    private readonly IFrameSource _FrameSource;
    private readonly ITrainingOptionsProvider _Options;
    private readonly ITrainingRandom _Random;

    public StartTrainingInteractor(ITrainingSessionStore sessions, ISettingsStore settings, IFrameSource frameSource,
        ITrainingOptionsProvider options, ITrainingRandom random)
    {
        _Sessions = sessions;
        _Settings = settings;
        _FrameSource = frameSource;
        _Options = options;
        _Random = random;
    }

    public async Task Handle(StartTrainingRequest request, IStartTrainingOutputPort output, CancellationToken ct)
    {
        if (_Sessions.HasActive)
        {
            await output.ReportStartFailed("A training session is already active.");
            return;
        }

        if (!_Settings.LoadTableConfig().TryGetValue(out TableConfiguration tableConfig))
        {
            await output.ReportStartFailed("No table configuration available.");
            return;
        }

        TrainingOptions options = _Options.LoadTrainingOptions();
        TrainingSession? session = null;
        bool started = false;
        string failureReason = "Configure frame source failed.";
        try
        {
            if (await _FrameSource.Configure(ct) == FrameSourceResult.Success)
            {
                BallTracker ballTracker = new(BallTrackerParams.Default, tableConfig);
                PossessionCalculator possession = new(tableConfig);
                LiveTracker liveTracker = new(LiveTrackerParams.Default, ballTracker, possession);
                session = new(Guid.NewGuid(), liveTracker, tableConfig, options, _Random);
                _Sessions.SaveActive(session);
                failureReason = "Start frame source failed.";
                started = await _FrameSource.Start(ct) == FrameSourceResult.Success;
            }
        }
        finally
        {
            if (!started)
            {
                session?.Stop();
                if (session is not null)
                {
                    _Sessions.Clear();
                }

                await _FrameSource.Stop(CancellationToken.None);
            }
        }

        if (!started)
        {
            await output.ReportStartFailed(failureReason);
            return;
        }

        await output.ReportStarted(new(session!.Id));
    }
}
