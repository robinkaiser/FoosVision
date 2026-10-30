// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Services.GameTracking;
using FoosVision.Domain.Game.Services.ReplayDecision.Strategies;
using FoosVision.Domain.Game.Services.ReplayDecision;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.ValueObjects;
using NSubstitute;

namespace FoosVision.Domain.UnitTests.Game.GameTracking;

public class GameTrackerTests
{
    private const long _1s = 1000 * 1_000_000L;

    private static readonly BallPossession _AnyPossession = new(Team.A, PossessionArea.Defense);
    private readonly IBallTracker _BallTracker;
    private readonly IPossessionCalculator _PossessionCalculator;
    private readonly IReplayDecider _ReplayDecider;
    private readonly GameTracker _Testee;

    public GameTrackerTests()
    {
        _BallTracker = Substitute.For<IBallTracker>();
        _PossessionCalculator = Substitute.For<IPossessionCalculator>();
        _PossessionCalculator.Compute(Arg.Any<Point>()).Returns(_AnyPossession);
        _PossessionCalculator.FindClosestBarType(Arg.Any<Point>()).Returns(Option<BarType>.Some(BarType.A1));
        _ReplayDecider = Substitute.For<IReplayDecider>();
        _ReplayDecider.Decide(Arg.Any<Frame>(), Arg.Any<bool>(), Arg.Any<ReplayCandidate?>())
            .Returns(Option<ReplayAnchor>.None());
        _Testee = new(new LiveTracker(LiveTrackerParams.Default, _BallTracker, _PossessionCalculator), _ReplayDecider);
    }

    [Fact]
    public void Observed_ball_creates_live_snapshot_and_asks_replay_decider_with_candidate()
    {
        Frame frame = CreateFrame(1, 1);
        TrackedBall tracked = CreateTrackedBall(frame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        TrackedBall candidate = CreateTrackedBall(frame, 2, 140, 100, TrackingStatus.Predicted, TrackingConfidence.Low);
        ArrangeSnapshots(CreateSnapshot(frame, tracked, [candidate]));

        var snapshot = _Testee.ApplyObservations(frame, []);

        Assert.Equal(GameTrackingPhase.Live, snapshot.Phase);
        Assert.True(snapshot.IsBallFound);
        Assert.Equal(tracked.Position, snapshot.BallPosition);
        Assert.Equal(_AnyPossession, snapshot.Possession);
        Assert.Equal(_AnyPossession, snapshot.LastKnownPossession);
        Assert.Equal(0, snapshot.PossessionTimeMs);
        Assert.Equal(0, snapshot.LastObservedBallAgeMs);
        Assert.Equal(2, snapshot.BallCandidates.Count);
        _ReplayDecider.Received().Decide(
            frame,
            true,
            Arg.Is<ReplayCandidate?>(candidate => IsReplayCandidate(candidate, tracked, _AnyPossession, 0, BarType.A1)));
    }

    [Fact]
    public void Observed_ball_suggests_replay_when_replay_decider_returns_anchor()
    {
        Frame frame = CreateFrame(1, 1);
        TrackedBall tracked = CreateTrackedBall(frame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        ReplayAnchor anchor = new(frame, tracked.Position, _AnyPossession, 0, ReplayTriggerKind.SavedShot);
        ArrangeSnapshots(CreateSnapshot(frame, tracked));
        _ReplayDecider.Decide(frame, true, Arg.Any<ReplayCandidate?>())
            .Returns(Option<ReplayAnchor>.Some(anchor));

        var snapshot = _Testee.ApplyObservations(frame, []);

        Assert.Equal(GameTrackingPhase.ReplaySuggested, snapshot.Phase);
        Assert.True(snapshot.IsBallFound);
        Assert.True(snapshot.IsReplaySuggested);
        Assert.NotNull(snapshot.ReplayAnchor);
        ReplayAnchor replayAnchor = snapshot.ReplayAnchor!;
        Assert.Equal(anchor, replayAnchor);
        Assert.Equal(ReplayTriggerKind.SavedShot, replayAnchor.TriggerKind);
    }

    [Fact]
    public void Predicted_ball_is_temporarily_lost_and_asks_replay_decider_without_candidate()
    {
        Frame observedFrame = CreateFrame(1, 1);
        Frame predictedFrame = CreateFrame(2, 2);
        TrackedBall observed = CreateTrackedBall(observedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        TrackedBall predicted = CreateTrackedBall(predictedFrame, 1, 110, 100, TrackingStatus.Predicted, TrackingConfidence.Low);
        ArrangeSnapshots(
            CreateSnapshot(observedFrame, observed),
            CreateSnapshot(predictedFrame, predicted));

        _ = _Testee.ApplyObservations(observedFrame, []);
        var snapshot = _Testee.ApplyObservations(predictedFrame, []);

        Assert.Equal(GameTrackingPhase.BallTemporarilyLost, snapshot.Phase);
        Assert.False(snapshot.IsBallFound);
        Assert.Equal(predicted.Position, snapshot.BallPosition);
        Assert.Equal(_AnyPossession, snapshot.Possession);
        Assert.Equal(_AnyPossession, snapshot.LastKnownPossession);
        Assert.Equal(1000, snapshot.LastObservedBallAgeMs);
        Assert.Single(snapshot.BallCandidates);
        Assert.Equal(TrackingStatus.Predicted, snapshot.BallCandidates[0].Status);
        _ReplayDecider.Received().Decide(
            predictedFrame,
            false,
            null);
    }

    [Fact]
    public void Temporarily_lost_ball_advances_possession_time_while_possession_is_held()
    {
        Frame firstObservedFrame = CreateFrame(1, 1);
        Frame secondObservedFrame = CreateFrame(2, 2);
        Frame predictedFrame = CreateFrame(3, 3);
        TrackedBall firstObserved = CreateTrackedBall(firstObservedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        TrackedBall secondObserved = CreateTrackedBall(secondObservedFrame, 1, 105, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        TrackedBall predicted = CreateTrackedBall(predictedFrame, 1, 110, 100, TrackingStatus.Predicted, TrackingConfidence.Low);
        ArrangeSnapshots(
            CreateSnapshot(firstObservedFrame, firstObserved),
            CreateSnapshot(secondObservedFrame, secondObserved),
            CreateSnapshot(predictedFrame, predicted));

        _ = _Testee.ApplyObservations(firstObservedFrame, []);
        var observedSnapshot = _Testee.ApplyObservations(secondObservedFrame, []);
        var temporarilyLostSnapshot = _Testee.ApplyObservations(predictedFrame, []);

        Assert.Equal(1000, observedSnapshot.PossessionTimeMs);
        Assert.Equal(GameTrackingPhase.BallTemporarilyLost, temporarilyLostSnapshot.Phase);
        Assert.False(temporarilyLostSnapshot.IsBallFound);
        Assert.Equal(_AnyPossession, temporarilyLostSnapshot.Possession);
        Assert.Equal(_AnyPossession, temporarilyLostSnapshot.LastKnownPossession);
        Assert.Equal(2000, temporarilyLostSnapshot.PossessionTimeMs);
        Assert.Equal(1000, temporarilyLostSnapshot.LastObservedBallAgeMs);
    }

    [Fact]
    public void Observed_ball_after_temporary_loss_includes_held_possession_time()
    {
        Frame firstObservedFrame = CreateFrame(1, 1);
        Frame secondObservedFrame = CreateFrame(2, 2);
        Frame predictedFrame = CreateFrame(3, 3);
        Frame recoveredFrame = CreateFrame(4, 4);
        TrackedBall firstObserved = CreateTrackedBall(firstObservedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        TrackedBall secondObserved = CreateTrackedBall(secondObservedFrame, 1, 105, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        TrackedBall predicted = CreateTrackedBall(predictedFrame, 1, 110, 100, TrackingStatus.Predicted, TrackingConfidence.Low);
        TrackedBall recovered = CreateTrackedBall(recoveredFrame, 1, 115, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        ArrangeSnapshots(
            CreateSnapshot(firstObservedFrame, firstObserved),
            CreateSnapshot(secondObservedFrame, secondObserved),
            CreateSnapshot(predictedFrame, predicted),
            CreateSnapshot(recoveredFrame, recovered));

        _ = _Testee.ApplyObservations(firstObservedFrame, []);
        _ = _Testee.ApplyObservations(secondObservedFrame, []);
        _ = _Testee.ApplyObservations(predictedFrame, []);
        var recoveredSnapshot = _Testee.ApplyObservations(recoveredFrame, []);

        Assert.Equal(GameTrackingPhase.Live, recoveredSnapshot.Phase);
        Assert.True(recoveredSnapshot.IsBallFound);
        Assert.Equal(_AnyPossession, recoveredSnapshot.Possession);
        Assert.Equal(_AnyPossession, recoveredSnapshot.LastKnownPossession);
        Assert.Equal(3000, recoveredSnapshot.PossessionTimeMs);
        Assert.Equal(0, recoveredSnapshot.LastObservedBallAgeMs);
    }

    [Fact]
    public void Default_windows_retire_visual_ball_before_possession_hold_expires()
    {
        BallTrackerParams ballTrackerParams = BallTrackerParams.Default;
        LiveTrackerParams liveTrackerParams = LiveTrackerParams.Default;
        BallTracker ballTracker = new(ballTrackerParams, TableConfig.Config);
        PossessionCalculator possessionCalculator = new(TableConfig.Config);
        GameTracker testee = new(
            new LiveTracker(liveTrackerParams, ballTracker, possessionCalculator),
            new ReplayDecider([new BallDisappearedReplayStrategy(TableConfig.Config)]));
        Frame observedFrame = new(1, _1s);
        Frame visuallyLostFrame = new(2, _1s + 600_000_000L);

        var observedSnapshot = testee.ApplyObservations(
            observedFrame,
            [new ObservedBall(new Point(200, 300), ObservationQualityThresholds.Default.HighQuality)]);
        var visuallyLostSnapshot = testee.ApplyObservations(visuallyLostFrame, []);

        Assert.Equal(TimeSpan.FromMilliseconds(500), ballTrackerParams.TrackedBallMaxUnobservedTime);
        Assert.Equal(TimeSpan.FromSeconds(2), liveTrackerParams.PossessionHoldTime);
        Assert.Equal(GameTrackingPhase.Live, observedSnapshot.Phase);
        Assert.Equal(GameTrackingPhase.BallTemporarilyLost, visuallyLostSnapshot.Phase);
        Assert.False(visuallyLostSnapshot.IsBallFound);
        Assert.Empty(visuallyLostSnapshot.BallCandidates);
        Assert.Equal(observedSnapshot.Possession, visuallyLostSnapshot.Possession);
        Assert.Equal(observedSnapshot.LastKnownPossession, visuallyLostSnapshot.LastKnownPossession);
        Assert.Equal(600, visuallyLostSnapshot.PossessionTimeMs);
        Assert.Equal(600, visuallyLostSnapshot.LastObservedBallAgeMs);
    }

    [Fact]
    public void Ball_lost_before_hold_time_suggests_goal_check_when_replay_anchor_exists()
    {
        GameTracker testee = CreateTesteeWithHoldTime(TimeSpan.FromSeconds(2));
        Frame observedFrame = CreateFrame(1, 1);
        Frame lostFrame = CreateFrame(2, 2);
        TrackedBall observed = CreateTrackedBall(observedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.High);
        ReplayAnchor anchor = new(observedFrame, observed.Position, _AnyPossession, 0, ReplayTriggerKind.BallDisappeared);
        ArrangeSnapshots(
            CreateSnapshot(observedFrame, observed),
            CreateSnapshot(lostFrame, null));
        _ReplayDecider.Decide(lostFrame, false, null).Returns(Option<ReplayAnchor>.Some(anchor));

        _ = testee.ApplyObservations(observedFrame, []);
        var snapshot = testee.ApplyObservations(lostFrame, []);

        Assert.Equal(GameTrackingPhase.ReplaySuggested, snapshot.Phase);
        Assert.False(snapshot.IsBallFound);
        Assert.True(snapshot.IsReplaySuggested);
        Assert.Equal(anchor, snapshot.ReplayAnchor);
        Assert.Equal(BallPossession.None, snapshot.Possession);
        Assert.Equal(_AnyPossession, snapshot.LastKnownPossession);
        _ReplayDecider.Received().Decide(lostFrame, false, null);
    }

    [Fact]
    public void Predicted_track_does_not_extend_goal_check_delay()
    {
        GameTracker testee = CreateTesteeWithHoldTime(TimeSpan.FromSeconds(2));
        Frame observedFrame = CreateFrame(1, 1);
        Frame predictedFrame = CreateFrame(2, 2);
        TrackedBall observed = CreateTrackedBall(observedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.High);
        TrackedBall predicted1 = CreateTrackedBall(predictedFrame, 1, 110, 100, TrackingStatus.Predicted, TrackingConfidence.Low);
        ReplayAnchor anchor = new(observedFrame, observed.Position, _AnyPossession, 0, ReplayTriggerKind.BallDisappeared);
        ArrangeSnapshots(
            CreateSnapshot(observedFrame, observed),
            CreateSnapshot(predictedFrame, predicted1));
        _ReplayDecider.Decide(predictedFrame, false, null).Returns(Option<ReplayAnchor>.Some(anchor));

        _ = testee.ApplyObservations(observedFrame, []);
        var replaySnapshot = testee.ApplyObservations(predictedFrame, []);

        Assert.Equal(GameTrackingPhase.ReplaySuggested, replaySnapshot.Phase);
        Assert.False(replaySnapshot.IsBallFound);
        Assert.Equal(1000, replaySnapshot.LastObservedBallAgeMs);
        Assert.True(replaySnapshot.IsReplaySuggested);
        Assert.Equal(anchor, replaySnapshot.ReplayAnchor);
        _ReplayDecider.Received().Decide(predictedFrame, false, null);
    }

    [Fact]
    public void Goal_check_decision_is_asked_during_each_lost_frame()
    {
        GameTracker testee = CreateTesteeWithHoldTime(TimeSpan.FromSeconds(2));
        Frame observedFrame = CreateFrame(1, 1);
        Frame firstLostFrame = CreateFrame(2, 3);
        Frame secondLostFrame = CreateFrame(3, 4);
        TrackedBall observed = CreateTrackedBall(observedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.High);
        ReplayAnchor anchor = new(observedFrame, observed.Position, _AnyPossession, 0, ReplayTriggerKind.BallDisappeared);
        ArrangeSnapshots(
            CreateSnapshot(observedFrame, observed),
            CreateSnapshot(firstLostFrame, null),
            CreateSnapshot(secondLostFrame, null));
        _ReplayDecider.Decide(firstLostFrame, false, null).Returns(Option<ReplayAnchor>.Some(anchor));

        _ = testee.ApplyObservations(observedFrame, []);
        var firstLostSnapshot = testee.ApplyObservations(firstLostFrame, []);
        var secondLostSnapshot = testee.ApplyObservations(secondLostFrame, []);

        Assert.Equal(GameTrackingPhase.ReplaySuggested, firstLostSnapshot.Phase);
        Assert.True(firstLostSnapshot.IsReplaySuggested);
        Assert.Equal(anchor, firstLostSnapshot.ReplayAnchor);
        Assert.Equal(GameTrackingPhase.BallLost, secondLostSnapshot.Phase);
        Assert.False(secondLostSnapshot.IsReplaySuggested);
        Assert.Null(secondLostSnapshot.ReplayAnchor);
        _ReplayDecider.Received(2).Decide(Arg.Any<Frame>(), false, null);
    }

    [Fact]
    public void Ball_lost_after_hold_time_without_replay_anchor_is_ball_lost()
    {
        GameTracker testee = CreateTesteeWithHoldTime(TimeSpan.FromSeconds(2));
        Frame observedFrame = CreateFrame(1, 1);
        Frame lostFrame = CreateFrame(2, 3);
        TrackedBall observed = CreateTrackedBall(observedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.High);
        ArrangeSnapshots(
            CreateSnapshot(observedFrame, observed),
            CreateSnapshot(lostFrame, null));
        _ReplayDecider.Decide(lostFrame, false, null).Returns(Option<ReplayAnchor>.None());

        _ = testee.ApplyObservations(observedFrame, []);
        var snapshot = testee.ApplyObservations(lostFrame, []);

        Assert.Equal(GameTrackingPhase.BallLost, snapshot.Phase);
        Assert.False(snapshot.IsBallFound);
        Assert.False(snapshot.IsReplaySuggested);
        Assert.Null(snapshot.ReplayAnchor);
        Assert.Equal(BallPossession.None, snapshot.Possession);
        Assert.Equal(_AnyPossession, snapshot.LastKnownPossession);
        _ReplayDecider.Received().Decide(lostFrame, false, null);
    }

    [Fact]
    public void Update_table_config_only_delegates_table_config()
    {
        _Testee.UpdateTableConfig(TableConfig.Config);

        _BallTracker.Received().UpdateTableConfig(TableConfig.Config);
        _PossessionCalculator.Received().UpdateTableConfig(TableConfig.Config);
        _ReplayDecider.Received().UpdateTableConfig(TableConfig.Config);
    }

    [Fact]
    public void Update_table_config_preserves_tracking_state()
    {
        Frame observedFrame = CreateFrame(1, 1);
        Frame predictedFrame = CreateFrame(2, 2);
        TrackedBall observed = CreateTrackedBall(observedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.Average);
        TrackedBall predicted = CreateTrackedBall(predictedFrame, 1, 110, 100, TrackingStatus.Predicted, TrackingConfidence.Low);
        ArrangeSnapshots(
            CreateSnapshot(observedFrame, observed),
            CreateSnapshot(predictedFrame, predicted));

        _ = _Testee.ApplyObservations(observedFrame, []);
        _Testee.UpdateTableConfig(TableConfig.Config);
        var snapshot = _Testee.ApplyObservations(predictedFrame, []);

        Assert.Equal(GameTrackingPhase.BallTemporarilyLost, snapshot.Phase);
        Assert.False(snapshot.IsBallFound);
        Assert.Equal(_AnyPossession, snapshot.LastKnownPossession);
        Assert.Equal(1000, snapshot.LastObservedBallAgeMs);
    }

    [Theory]
    [InlineData(PossessionArea.Defense, 15000)]
    [InlineData(PossessionArea.FiveBar, 10000)]
    [InlineData(PossessionArea.ThreeBar, 15000)]
    [InlineData(PossessionArea.None, 0)]
    public void Game_applies_time_foul_only_after_area_limit(PossessionArea area, int limitMs)
    {
        _PossessionCalculator.Compute(Arg.Any<Point>()).Returns(new BallPossession(Team.A, area));
        Frame start = new(1, _1s);
        Frame atLimit = new(2, _1s + limitMs * 1_000_000L);
        Frame afterLimit = new(3, atLimit.TimestampNs + 1_000_000L);
        ArrangeSnapshots(
            CreateSnapshot(start, CreateTrackedBall(start, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.High)),
            CreateSnapshot(atLimit, CreateTrackedBall(atLimit, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.High)),
            CreateSnapshot(afterLimit, CreateTrackedBall(afterLimit, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.High)));

        _Testee.ApplyObservations(start, []);
        GameTrackingSnapshot atLimitSnapshot = _Testee.ApplyObservations(atLimit, []);
        GameTrackingSnapshot afterLimitSnapshot = _Testee.ApplyObservations(afterLimit, []);

        Assert.False(atLimitSnapshot.IsTimeFoul);
        Assert.Equal(area != PossessionArea.None, afterLimitSnapshot.IsTimeFoul);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 3000)]
    public void Replay_precedence_preserves_possession_timer_on_expired_loss(bool suggestReplay, int recoveredTimeMs)
    {
        Frame observedFrame = CreateFrame(1, 1);
        Frame lostFrame = CreateFrame(2, 3);
        Frame recoveredFrame = CreateFrame(3, 4);
        TrackedBall observed = CreateTrackedBall(observedFrame, 1, 100, 100, TrackingStatus.Observed, TrackingConfidence.High);
        TrackedBall recovered = observed with { Frame = recoveredFrame };
        ReplayAnchor anchor = new(observedFrame, observed.Position, _AnyPossession, 0, ReplayTriggerKind.BallDisappeared);
        ArrangeSnapshots(CreateSnapshot(observedFrame, observed), CreateSnapshot(lostFrame, null),
            CreateSnapshot(recoveredFrame, recovered));
        _ReplayDecider.Decide(lostFrame, false, null)
            .Returns(suggestReplay ? Option<ReplayAnchor>.Some(anchor) : Option<ReplayAnchor>.None());

        _Testee.ApplyObservations(observedFrame, []);
        GameTrackingSnapshot lost = _Testee.ApplyObservations(lostFrame, []);
        GameTrackingSnapshot recovery = _Testee.ApplyObservations(recoveredFrame, []);

        Assert.Equal(suggestReplay, lost.IsReplaySuggested);
        Assert.Equal(suggestReplay ? anchor : null, lost.ReplayAnchor);
        Assert.Equal(BallPossession.None, lost.Possession);
        Assert.Equal(0, lost.PossessionTimeMs);
        Assert.False(lost.IsTimeFoul);
        Assert.Equal(recoveredTimeMs, recovery.PossessionTimeMs);
    }

    private GameTracker CreateTesteeWithHoldTime(TimeSpan holdTime)
    {
        return new(
            new LiveTracker(LiveTrackerParams.Default with { PossessionHoldTime = holdTime },
                _BallTracker, _PossessionCalculator),
            _ReplayDecider);
    }

    private void ArrangeSnapshots(params TrackingSnapshot[] snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        Assert.NotEmpty(snapshots);

        _BallTracker.ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>())
            .Returns(snapshots[0], [.. snapshots.Skip(1)]);
    }

    private static Frame CreateFrame(ulong id, long seconds)
    {
        return new Frame(id, seconds * _1s);
    }

    private static TrackedBall CreateTrackedBall(
        Frame frame,
        int id,
        double x,
        double y,
        TrackingStatus status,
        TrackingConfidence confidence)
    {
        return new(
            id,
            frame,
            new Point(x, y),
            confidence,
            status,
            default);
    }

    private static TrackingSnapshot CreateSnapshot(
        Frame frame,
        TrackedBall? trackedBall,
        params IEnumerable<TrackedBall>[] otherCandidates)
    {
        List<TrackedBall> candidates = [];

        if (trackedBall != null)
        {
            candidates.Add(trackedBall);
        }

        candidates.AddRange(otherCandidates.SelectMany(static candidates => candidates));

        return new TrackingSnapshot(frame, candidates);
    }

    private static bool IsReplayCandidate(
        ReplayCandidate? candidate,
        TrackedBall trackedBall,
        BallPossession possession,
        int possessionTimeMs,
        BarType bar)
    {
        return candidate.HasValue &&
            candidate.Value.Frame == trackedBall.Frame &&
            candidate.Value.Position == trackedBall.Position &&
            candidate.Value.Possession == possession &&
            candidate.Value.PossessionTimeMs == possessionTimeMs &&
            candidate.Value.VelocityPxPerS == trackedBall.VelocityPxPerS &&
            candidate.Value.Confidence == trackedBall.Confidence &&
            candidate.Value.Bar == bar;
    }
}
