// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.UseCases.Training;

public record TrainingAnnouncementRequest(
    TrainingAttempt Attempt,
    long AnnouncementId,
    TrainingAnnouncementKind Kind,
    string Text,
    TrainingVariant? Variant);

public record TrainingAnnouncementInvalidation(TrainingAttempt Attempt, long ThroughAnnouncementId);

public record TrainingAttemptCompletion(
    TrainingAttempt Attempt,
    TrainingVariant Variant,
    Frame ConfirmationFrame,
    Frame CompletionFrame);

public record TrainingAttemptAbort(TrainingAttempt Attempt, TrainingAbortReason Reason);
