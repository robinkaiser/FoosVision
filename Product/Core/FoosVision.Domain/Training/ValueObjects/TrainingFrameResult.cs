// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.Training.Events;

namespace FoosVision.Domain.Training.ValueObjects;

public record TrainingFrameResult(
    LiveTrackingSnapshot Tracking,
    IReadOnlyList<TrainingEvent> Events,
    bool RequestTableConfigUpdate,
    bool RequestTableSceneUpdate,
    TrainingState State,
    TrainingAttempt? Attempt,
    TrainingVariant? Variant);
