// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.UseCases.Training.ProcessFrame;

/// <summary>
/// Apply announcement invalidation first, then preparation/attempt notifications, then the requested announcement.
/// Optional outputs describe this frame only; Status describes the current session state.
/// </summary>
public record ProcessFrameResponse(
    Guid SessionId,
    TrackingFrameResponse Tracking,
    TrainingStatusResponse Status,
    TrainingAttempt? PreparationConfirmed,
    TrainingAnnouncementRequest? Announcement,
    TrainingAnnouncementInvalidation? AnnouncementInvalidation,
    TrainingAttemptCompletion? CompletedAttempt,
    TrainingAttemptAbort? AbortedAttempt);

public record TrackingFrameResponse(
    Frame Frame,
    bool IsBallFound,
    Point BallPosition,
    Vector2 BallVelocityPxPerS,
    IReadOnlyList<TrackedBall> BallCandidates,
    IReadOnlyList<ObservedBall> Observations,
    BallPossession Possession,
    BallPossession LastKnownPossession,
    int PossessionTimeMs,
    LiveTrackingPhase TrackingPhase,
    int LastObservedBallAgeMs,
    BarType? ClosestBar,
    bool RequestTableConfigUpdate,
    bool RequestTableSceneUpdate);

public record TrainingStatusResponse(
    TrainingState State,
    TrainingAttempt? Attempt,
    TrainingVariant? Variant);

public interface IProcessFrameOutputPort
{
    Task ReportProcessed(ProcessFrameResponse response);
    Task ReportSkipped(string reason);
}
