// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.GameTracking;

public class GameTracker : IGameTracker
{
    private readonly GameTrackerParams _Params;
    private readonly IBallTracker _BallTracker;
    private readonly IPossessionCalculator _PossessionCalculator;
    private readonly IReplayDecider _ReplayDecider;

    private bool _HasObservedBall;
    private Frame _LastObservedBallFrame;
    private BallPossession _LastPossession;
    private BallPossession _LastKnownPossession;
    private long _PossessionStartTimestampNs;
    private int _LastPossessionTimeMs;

    public GameTracker(
        GameTrackerParams parameters,
        IBallTracker ballTracker,
        IPossessionCalculator possessionCalculator,
        IReplayDecider replayDecider)
    {
        _Params = parameters;
        _BallTracker = ballTracker;
        _PossessionCalculator = possessionCalculator;
        _ReplayDecider = replayDecider;
        ResetCurrentPossession();
    }

    public GameTrackingSnapshot? Latest { get; private set; }

    public GameTrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
    {
        var visualSnapshot = _BallTracker.ApplyObservations(frame, observations);
        var observedBall = visualSnapshot.Candidates.FirstOrDefault(static c => c.Status == TrackingStatus.Observed);

        Latest = observedBall == null
            ? ApplyUnobservedFrame(frame, visualSnapshot.Candidates)
            : ApplyObservedFrame(frame, observedBall, visualSnapshot.Candidates);

        return Latest;
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        _BallTracker.UpdateTableConfig(tableConfig);
        _PossessionCalculator.UpdateTableConfig(tableConfig);
        _ReplayDecider.UpdateTableConfig(tableConfig);
    }

    private GameTrackingSnapshot ApplyObservedFrame(
        Frame frame,
        TrackedBall trackedBall,
        IReadOnlyList<TrackedBall> candidates)
    {
        var possession = _PossessionCalculator.Compute(trackedBall.Position);
        int possessionTimeMs = UpdatePossessionTime(frame, possession);
        bool isTimeFoul = IsTimeFoul(possession.Area, possessionTimeMs);

        _HasObservedBall = true;
        _LastObservedBallFrame = trackedBall.Frame;

        if (possession != BallPossession.None)
        {
            _LastKnownPossession = possession;
        }

        ReplayCandidate? replayCandidate = null;

        if (_PossessionCalculator.FindClosestBarType(trackedBall.Position).TryGetValue(out BarType bar))
        {
            replayCandidate = new ReplayCandidate(
                trackedBall.Frame,
                trackedBall.Position,
                possession,
                possessionTimeMs,
                trackedBall.VelocityPxPerS,
                trackedBall.Confidence,
                bar);
        }

        ReplayAnchor? replayAnchor = DecideReplay(trackedBall.Frame, true, replayCandidate);

        return GameTrackingSnapshotFactory.Observed(
            frame,
            trackedBall,
            candidates,
            possession,
            _LastKnownPossession,
            possessionTimeMs,
            isTimeFoul,
            replayAnchor);
    }

    private GameTrackingSnapshot ApplyUnobservedFrame(Frame frame, IReadOnlyList<TrackedBall> candidates)
    {
        var visualBall = candidates.Count > 0 ? candidates[0] : null;
        var position = visualBall?.Position ?? default;
        var velocity = visualBall?.VelocityPxPerS ?? default;
        int lastObservedBallAgeMs = GetLastObservedBallAgeMs(frame);

        ReplayAnchor? replayAnchor = DecideReplay(frame, false, null);

        if (replayAnchor != null)
        {
            return GameTrackingSnapshotFactory.Lost(
                frame,
                position,
                velocity,
                candidates,
                _LastKnownPossession,
                lastObservedBallAgeMs,
                replayAnchor);
        }

        if (!_HasObservedBall)
        {
            return GameTrackingSnapshotFactory.Lost(
                frame,
                position,
                velocity,
                candidates,
                _LastKnownPossession,
                lastObservedBallAgeMs,
                replayAnchor: null);
        }

        long lostTimeNs = frame.TimestampNs - _LastObservedBallFrame.TimestampNs;
        bool isTemporarilyLost = lostTimeNs < (long)_Params.PossessionHoldTime.TotalNanoseconds;

        if (isTemporarilyLost)
        {
            int possessionTimeMs = GetCurrentPossessionTimeMs(frame);
            return GameTrackingSnapshotFactory.TemporarilyLost(
                frame,
                position,
                velocity,
                candidates,
                _LastKnownPossession,
                possessionTimeMs,
                lastObservedBallAgeMs,
                IsTimeFoul(_LastKnownPossession.Area, possessionTimeMs));
        }

        ResetCurrentPossession();

        return GameTrackingSnapshotFactory.Lost(
            frame,
            position,
            velocity,
            candidates,
            _LastKnownPossession,
            lastObservedBallAgeMs,
            replayAnchor: null);
    }

    private ReplayAnchor? DecideReplay(Frame frame, bool isBallObserved, ReplayCandidate? candidate)
    {
        var anchor = _ReplayDecider.Decide(frame, isBallObserved, candidate);

        return anchor.TryGetValue(out ReplayAnchor? replayAnchor)
            ? replayAnchor
            : null;
    }

    private int GetLastObservedBallAgeMs(Frame frame)
    {
        if (!_HasObservedBall)
        {
            return 0;
        }

        long elapsedNs = frame.TimestampNs - _LastObservedBallFrame.TimestampNs;
        if (elapsedNs <= 0)
        {
            return 0;
        }

        return (int)(elapsedNs / 1_000_000L);
    }

    private void ResetPossession()
    {
        _LastKnownPossession = BallPossession.None;
        ResetCurrentPossession();
    }

    private void ResetCurrentPossession()
    {
        _LastPossession = BallPossession.None;
        _PossessionStartTimestampNs = 0;
        _LastPossessionTimeMs = 0;
    }

    private int UpdatePossessionTime(Frame frame, BallPossession possession)
    {
        if (possession == BallPossession.None)
        {
            ResetPossession();
            return 0;
        }

        if (_LastPossession != possession)
        {
            _LastPossession = possession;
            _PossessionStartTimestampNs = frame.TimestampNs;
            _LastPossessionTimeMs = 0;
            return 0;
        }

        return GetCurrentPossessionTimeMs(frame);
    }

    private int GetCurrentPossessionTimeMs(Frame frame)
    {
        if (_LastPossession == BallPossession.None)
        {
            _LastPossessionTimeMs = 0;
            return 0;
        }

        long elapsedNs = frame.TimestampNs - _PossessionStartTimestampNs;
        if (elapsedNs <= 0)
        {
            _LastPossessionTimeMs = 0;
            return 0;
        }

        _LastPossessionTimeMs = (int)(elapsedNs / 1_000_000L);
        return _LastPossessionTimeMs;
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
