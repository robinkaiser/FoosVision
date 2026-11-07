// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Replay.ValueObjects;

/// <summary>Repeat locally until the recorder requests live, after at least the required loops.</summary>
public record ReplayPlan(
    Frame AnchorFrame,
    Point AnchorPosition,
    BallPossession AnchorPossession,
    int AnchorPossessionTimeMs,
    long StartTimestampNs,
    long EndTimestampNs,
    double Speed,
    int RequiredCompletedLoops);
