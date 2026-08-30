// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.GameTracking;

internal static class GameTrackingSnapshotFactory
{
    public static GameTrackingSnapshot Observed(
        Frame frame,
        TrackedBall trackedBall,
        IReadOnlyList<TrackedBall> candidates,
        BallPossession possession,
        BallPossession lastKnownPossession,
        int possessionTimeMs,
        bool isTimeFoul,
        ReplayAnchor? replayAnchor)
    {
        return Create(
            frame,
            replayAnchor == null ? GameTrackingPhase.Live : GameTrackingPhase.ReplaySuggested,
            true,
            trackedBall.Position,
            trackedBall.VelocityPxPerS,
            candidates,
            possession,
            lastKnownPossession,
            possessionTimeMs,
            0,
            isTimeFoul,
            replayAnchor);
    }

    public static GameTrackingSnapshot TemporarilyLost(
        Frame frame,
        Point position,
        Vector2 velocity,
        IReadOnlyList<TrackedBall> candidates,
        BallPossession heldPossession,
        int possessionTimeMs,
        int lastObservedBallAgeMs,
        bool isTimeFoul)
    {
        return Create(
            frame,
            GameTrackingPhase.BallTemporarilyLost,
            false,
            position,
            velocity,
            candidates,
            heldPossession,
            heldPossession,
            possessionTimeMs,
            lastObservedBallAgeMs,
            isTimeFoul,
            null);
    }

    public static GameTrackingSnapshot Lost(
        Frame frame,
        Point position,
        Vector2 velocity,
        IReadOnlyList<TrackedBall> candidates,
        BallPossession lastKnownPossession,
        int lastObservedBallAgeMs,
        ReplayAnchor? replayAnchor)
    {
        return Create(
            frame,
            replayAnchor == null ? GameTrackingPhase.BallLost : GameTrackingPhase.ReplaySuggested,
            false,
            position,
            velocity,
            candidates,
            BallPossession.None,
            lastKnownPossession,
            0,
            lastObservedBallAgeMs,
            false,
            replayAnchor);
    }

    private static GameTrackingSnapshot Create(
        Frame frame,
        GameTrackingPhase phase,
        bool isBallFound,
        Point ballPosition,
        Vector2 ballVelocityPxPerS,
        IReadOnlyList<TrackedBall> ballCandidates,
        BallPossession possession,
        BallPossession lastKnownPossession,
        int possessionTimeMs,
        int lastObservedBallAgeMs,
        bool isTimeFoul,
        ReplayAnchor? replayAnchor)
    {
        bool isReplaySuggested = replayAnchor != null;

        return new(
            frame,
            phase,
            isBallFound,
            ballPosition,
            ballVelocityPxPerS,
            ballCandidates,
            possession,
            lastKnownPossession,
            possessionTimeMs,
            lastObservedBallAgeMs,
            isTimeFoul,
            isReplaySuggested,
            replayAnchor);
    }
}
