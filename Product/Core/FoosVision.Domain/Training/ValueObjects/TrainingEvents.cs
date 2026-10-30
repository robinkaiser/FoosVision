// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Training.ValueObjects;

public enum TrainingState
{
    Searching,
    Waiting,
    AwaitingExecution,
    Evaluating,
    Completed,
    Aborted,
    Stopped,
}

public enum TrainingAbortReason
{
    PrematureExecution,
    PreparationLost,
    ExecutionTimeout,
    BallLost,
}

public enum TrainingAnnouncementKind
{
    Ready,
    Variant,
    Aborted,
}

public record TrainingVariant(string Id, string Text);

public record TrainingAnchor(Frame Frame, Point Position, BallPossession Possession);

public record TrainingAttempt(
    Guid SessionId,
    long AttemptId,
    TrainingStrategyKind Strategy,
    Team Team,
    BarType Bar,
    TrainingAnchor Anchor);

public abstract record TrainingEvent(TrainingAttempt Attempt);

/// <summary>Invalidates older announcements and replay requests when a new preparation is confirmed.</summary>
public record TrainingPreparationConfirmed(TrainingAttempt Attempt) : TrainingEvent(Attempt);

public record TrainingAnnouncementRequested(
    TrainingAttempt Attempt,
    long AnnouncementId,
    TrainingAnnouncementKind Kind,
    string Text,
    TrainingVariant? Variant = null) : TrainingEvent(Attempt);

/// <summary>Discard pending/playing announcements through this ID; the following Aborted remains valid.</summary>
public record TrainingAnnouncementsInvalidated(TrainingAttempt Attempt, long ThroughAnnouncementId) : TrainingEvent(Attempt);

public record TrainingAttemptAborted(TrainingAttempt Attempt, TrainingAbortReason Reason) : TrainingEvent(Attempt);

/// <summary>A domain anchor only. Clip capture, transport and playback are implemented separately.</summary>
public record TrainingAttemptCompleted(
    TrainingAttempt Attempt,
    TrainingVariant Variant,
    Frame ConfirmationFrame,
    Frame CompletionFrame) : TrainingEvent(Attempt);
