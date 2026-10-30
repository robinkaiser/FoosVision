// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Game.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Game.Services.GameTracking;

internal static class GameTrackingSnapshotFactory
{
    public static GameTrackingSnapshot Create(LiveTrackingSnapshot tracking, ReplayAnchor? replayAnchor)
    {
        bool replayWhileLost = !tracking.IsBallFound && replayAnchor != null;
        BallPossession possession = replayWhileLost ? BallPossession.None : tracking.Possession;
        int possessionTimeMs = replayWhileLost ? 0 : tracking.PossessionTimeMs;
        GameTrackingPhase phase = replayAnchor != null
            ? GameTrackingPhase.ReplaySuggested
            : tracking.Phase switch
            {
                LiveTrackingPhase.Live => GameTrackingPhase.Live,
                LiveTrackingPhase.BallTemporarilyLost => GameTrackingPhase.BallTemporarilyLost,
                _ => GameTrackingPhase.BallLost,
            };

        return new(tracking.Frame, phase, tracking.IsBallFound, tracking.BallPosition,
            tracking.BallVelocityPxPerS, tracking.BallCandidates, possession,
            tracking.LastKnownPossession, possessionTimeMs, tracking.LastObservedBallAgeMs,
            IsTimeFoul(possession.Area, possessionTimeMs), replayAnchor != null, replayAnchor);
    }

    private static bool IsTimeFoul(PossessionArea area, int possessionTimeMs)
    {
        return area switch
        {
            PossessionArea.Defense => possessionTimeMs > 15000,
            PossessionArea.FiveBar => possessionTimeMs > 10000,
            PossessionArea.ThreeBar => possessionTimeMs > 15000,
            _ => false,
        };
    }
}
