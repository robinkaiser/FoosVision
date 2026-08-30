// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.GameTracking;

public enum GameTrackingPhase
{
    Live,
    BallTemporarilyLost,
    BallLost,
    ReplaySuggested,
    Timeout,
}

public record GameTrackingSnapshot(
    Frame Frame,
    GameTrackingPhase Phase,
    bool IsBallFound,
    Point BallPosition,
    Vector2 BallVelocityPxPerS,
    IReadOnlyList<TrackedBall> BallCandidates,
    BallPossession Possession,
    BallPossession LastKnownPossession,
    int PossessionTimeMs,
    int LastObservedBallAgeMs,
    bool IsTimeFoul,
    bool IsReplaySuggested,
    ReplayAnchor? ReplayAnchor);
