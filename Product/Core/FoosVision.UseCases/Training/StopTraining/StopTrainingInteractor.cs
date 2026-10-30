// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.Training.StopTraining;

public class StopTrainingInteractor : IStopTrainingInputPort
{
    private readonly ITrainingSessionStore _Sessions;
    private readonly IFrameSource _FrameSource;

    public StopTrainingInteractor(ITrainingSessionStore sessions, IFrameSource frameSource)
    {
        _Sessions = sessions;
        _FrameSource = frameSource;
    }

    public async Task Handle(StopTrainingRequest request, IStopTrainingOutputPort output, CancellationToken ct)
    {
        if (!_Sessions.LoadActive().TryGetValue(out TrainingSession session))
        {
            await output.ReportStopFailed("No active session.");
            return;
        }

        IReadOnlyList<TrainingEvent> events = session.Stop();
        TrainingAnnouncementInvalidation? invalidation = null;
        foreach (TrainingEvent change in events)
        {
            if (change is TrainingAnnouncementsInvalidated invalidated)
            {
                invalidation = new(invalidated.Attempt, invalidated.ThroughAnnouncementId);
            }
        }

        _Sessions.Clear();
        await _FrameSource.Stop(ct);
        StopTrainingResponse response = new(session.Id, invalidation);
        await output.ReportStopped(response);
    }
}
