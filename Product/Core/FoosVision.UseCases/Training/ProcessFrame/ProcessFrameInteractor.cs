// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Training.Services;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.Events;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.Training.ProcessFrame;

public class ProcessFrameInteractor : IProcessFrameInputPort
{
    private readonly ITrainingSessionStore _SessionStore;

    public ProcessFrameInteractor(ITrainingSessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(ProcessFrameRequest request, IProcessFrameOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out TrainingSession session))
        {
            await output.ReportSkipped("No active session.");
            return;
        }

        var observed = request.Vision.DetectBalls(session.TableConfig);
        TrainingFrameResult result = session.ApplyObservations(request.Frame, observed);
        LiveTrackingSnapshot tracking = result.Tracking;
        TrainingAttempt? preparationConfirmed = null;
        TrainingAnnouncementInvalidation? invalidation = null;
        TrainingAnnouncementRequest? announcementRequest = null;
        TrainingAttemptAbort? abortedAttempt = null;
        TrainingAttemptCompletion? completedAttempt = null;
        Frame? returnToLiveReplayFrame = null;
        ReplayPlan? replay = null;

        foreach (TrainingEvent change in result.Events)
        {
            switch (change)
            {
                case TrainingReturnToLiveRequested returnToLive:
                    returnToLiveReplayFrame = returnToLive.ReplayTriggerFrame;
                    break;

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
                    replay = TrainingReplayRules.Create(completed);
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
            [.. observed],
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
            preparationConfirmed, announcementRequest, invalidation, completedAttempt, abortedAttempt, returnToLiveReplayFrame, replay);

        await output.ReportProcessed(response);
    }
}
