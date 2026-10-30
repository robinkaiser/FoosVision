// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.LiveTracking;

public enum LiveTrackingPhase
{
    Live,
    BallTemporarilyLost,
    BallLost,
}

/// <summary>Live tracking facts without mode-specific foul or replay decisions.</summary>
public record LiveTrackingSnapshot(
    Frame Frame,
    LiveTrackingPhase Phase,
    bool IsBallFound,
    Point BallPosition,
    Vector2 BallVelocityPxPerS,
    IReadOnlyList<TrackedBall> BallCandidates,
    BallPossession Possession,
    BallPossession LastKnownPossession,
    int PossessionTimeMs,
    int LastObservedBallAgeMs,
    BarType? ClosestBar);
