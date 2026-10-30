// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.LiveTracking;

internal static class LiveTrackingSnapshotFactory
{
    public static LiveTrackingSnapshot Observed(
        Frame frame,
        TrackedBall trackedBall,
        IReadOnlyList<TrackedBall> candidates,
        BallPossession possession,
        BallPossession lastKnownPossession,
        int possessionTimeMs,
        BarType? closestBar)
    {
        return new(frame, LiveTrackingPhase.Live, true, trackedBall.Position,
            trackedBall.VelocityPxPerS, candidates, possession, lastKnownPossession,
            possessionTimeMs, 0, closestBar);
    }

    public static LiveTrackingSnapshot TemporarilyLost(
        Frame frame,
        Point position,
        Vector2 velocity,
        IReadOnlyList<TrackedBall> candidates,
        BallPossession heldPossession,
        int possessionTimeMs,
        int lastObservedBallAgeMs)
    {
        return new(frame, LiveTrackingPhase.BallTemporarilyLost, false, position,
            velocity, candidates, heldPossession, heldPossession,
            possessionTimeMs, lastObservedBallAgeMs, null);
    }

    public static LiveTrackingSnapshot Lost(
        Frame frame,
        Point position,
        Vector2 velocity,
        IReadOnlyList<TrackedBall> candidates,
        BallPossession lastKnownPossession,
        int lastObservedBallAgeMs)
    {
        return new(frame, LiveTrackingPhase.BallLost, false, position,
            velocity, candidates, BallPossession.None, lastKnownPossession,
            0, lastObservedBallAgeMs, null);
    }
}
