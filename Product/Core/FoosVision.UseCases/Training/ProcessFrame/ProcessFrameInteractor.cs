// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.Training.ProcessFrame;

public class ProcessFrameInteractor : IProcessFrameInputPort
{
    private readonly ITrainingSessionStore _Sessions;

    public ProcessFrameInteractor(ITrainingSessionStore sessions) => _Sessions = sessions;

    public async Task Handle(ProcessFrameRequest request, IProcessFrameOutputPort output, CancellationToken ct)
    {
        if (!_Sessions.LoadActive().TryGetValue(out TrainingSession session) || session.Id != request.SessionId)
        {
            await output.ReportSkipped("No matching active training session.");
            return;
        }

        ct.ThrowIfCancellationRequested();
        var observations = request.Vision.DetectBalls(session.TableConfig);
        ct.ThrowIfCancellationRequested();
        if (!_Sessions.LoadActive().TryGetValue(out TrainingSession current) || !ReferenceEquals(session, current))
        {
            await output.ReportSkipped("Training session changed during detection.");
            return;
        }

        TrainingFrameResult result = session.ApplyObservations(request.Frame, observations);
        LiveTrackingSnapshot tracking = result.Tracking;
        TrainingAttempt? preparationConfirmed = null;
        TrainingAnnouncementInvalidation? invalidation = null;
        TrainingAnnouncementRequest? announcementRequest = null;
        TrainingAttemptAbort? abortedAttempt = null;
        TrainingAttemptCompletion? completedAttempt = null;

        foreach (TrainingEvent change in result.Events)
        {
            switch (change)
            {
                case TrainingPreparationConfirmed confirmed:
                    preparationConfirmed = confirmed.Attempt;
                    break;

                case TrainingAnnouncementsInvalidated invalidated:
                    invalidation = new(invalidated.Attempt, invalidated.ThroughAnnouncementId);
                    break;

                case TrainingAnnouncementRequested announcement:
                    announcementRequest = new(announcement.Attempt, announcement.AnnouncementId,
                        announcement.Kind, announcement.Text, announcement.Variant);
                    break;

                case TrainingAttemptAborted aborted:
                    abortedAttempt = new(aborted.Attempt, aborted.Reason);
                    break;

                case TrainingAttemptCompleted completed:
                    completedAttempt = new(completed.Attempt, completed.Variant, completed.ConfirmationFrame, completed.CompletionFrame);
                    break;
            }
        }

        TrackingFrameResponse trackingResponse = new(
            tracking.Frame,
            tracking.IsBallFound,
            tracking.BallPosition,
            tracking.BallVelocityPxPerS,
            tracking.BallCandidates,
            [.. observations],
            tracking.Possession,
            tracking.LastKnownPossession,
            tracking.PossessionTimeMs,
            tracking.Phase,
            tracking.LastObservedBallAgeMs,
            tracking.ClosestBar,
            result.RequestTableConfigUpdate,
            result.RequestTableSceneUpdate);
        TrainingStatusResponse status = new(result.State, result.Attempt, result.Variant);
        ProcessFrameResponse response = new(session.Id, trackingResponse, status,
            preparationConfirmed, announcementRequest, invalidation, completedAttempt, abortedAttempt);

        await output.ReportProcessed(response);
    }
}
