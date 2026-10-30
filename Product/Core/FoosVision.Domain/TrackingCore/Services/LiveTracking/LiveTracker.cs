// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.LiveTracking;

/// <summary>Tracks live facts for one session. Create fresh tracking components for each session.</summary>
public class LiveTracker
{
    private readonly LiveTrackerParams _Params;
    private readonly IBallTracker _BallTracker;
    private readonly IPossessionCalculator _PossessionCalculator;

    private bool _HasObservedBall;
    private Frame _LastObservedBallFrame;
    private BallPossession _LastPossession;
    private BallPossession _LastKnownPossession = BallPossession.None;
    private long _PossessionStartTimestampNs;
    private int _LastPossessionTimeMs;

    public LiveTracker(
        LiveTrackerParams parameters,
        IBallTracker ballTracker,
        IPossessionCalculator possessionCalculator)
    {
        _Params = parameters;
        _BallTracker = ballTracker;
        _PossessionCalculator = possessionCalculator;
        ResetCurrentPossession();
    }

    public LiveTrackingSnapshot? Latest { get; private set; }

    public LiveTrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
    {
        LiveTrackingSnapshot snapshot = ObserveFrame(frame, observations);
        CompleteFrame();
        return snapshot;
    }

    // Allow the mode to consume this frame's facts before committing possession expiration.
    internal LiveTrackingSnapshot ObserveFrame(Frame frame, IEnumerable<ObservedBall> observations)
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
    }

    private LiveTrackingSnapshot ApplyObservedFrame(
        Frame frame,
        TrackedBall trackedBall,
        IReadOnlyList<TrackedBall> candidates)
    {
        var possession = _PossessionCalculator.Compute(trackedBall.Position);
        int possessionTimeMs = UpdatePossessionTime(frame, possession);

        _HasObservedBall = true;
        _LastObservedBallFrame = trackedBall.Frame;

        if (possession != BallPossession.None)
        {
            _LastKnownPossession = possession;
        }

        BarType? closestBar = _PossessionCalculator.FindClosestBarType(trackedBall.Position)
            .TryGetValue(out BarType bar) ? bar : null;

        return LiveTrackingSnapshotFactory.Observed(
            frame,
            trackedBall,
            candidates,
            possession,
            _LastKnownPossession,
            possessionTimeMs,
            closestBar);
    }

    private LiveTrackingSnapshot ApplyUnobservedFrame(Frame frame, IReadOnlyList<TrackedBall> candidates)
    {
        var visualBall = candidates.Count > 0 ? candidates[0] : null;
        var position = visualBall?.Position ?? default;
        var velocity = visualBall?.VelocityPxPerS ?? default;
        int lastObservedBallAgeMs = GetLastObservedBallAgeMs(frame);

        if (!_HasObservedBall)
        {
            return LiveTrackingSnapshotFactory.Lost(
                frame,
                position,
                velocity,
                candidates,
                _LastKnownPossession,
                lastObservedBallAgeMs);
        }

        long lostTimeNs = frame.TimestampNs - _LastObservedBallFrame.TimestampNs;
        bool isTemporarilyLost = lostTimeNs < (long)_Params.PossessionHoldTime.TotalNanoseconds;

        if (isTemporarilyLost)
        {
            int possessionTimeMs = GetCurrentPossessionTimeMs(frame);
            return LiveTrackingSnapshotFactory.TemporarilyLost(
                frame,
                position,
                velocity,
                candidates,
                _LastKnownPossession,
                possessionTimeMs,
                lastObservedBallAgeMs);
        }

        return LiveTrackingSnapshotFactory.Lost(
            frame,
            position,
            velocity,
            candidates,
            _LastKnownPossession,
            lastObservedBallAgeMs);
    }

    internal void CompleteFrame()
    {
        if (Latest?.Phase == LiveTrackingPhase.BallLost)
        {
            ResetCurrentPossession();
        }
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
}
