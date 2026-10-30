// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.ValueObjects;
using NSubstitute;

namespace FoosVision.Domain.UnitTests.TrackingCore.LiveTracking;

public class LiveTrackerTests
{
    private static readonly BallPossession _Possession = new(Team.A, PossessionArea.ThreeBar);
    private readonly IBallTracker _BallTracker = Substitute.For<IBallTracker>();
    private readonly IPossessionCalculator _Calculator = Substitute.For<IPossessionCalculator>();
    private readonly LiveTracker _Testee;

    public LiveTrackerTests()
    {
        _Calculator.Compute(Arg.Any<Point>()).Returns(_Possession);
        _Calculator.FindClosestBarType(Arg.Any<Point>()).Returns(Option<BarType>.Some(BarType.A3));
        _Testee = new(LiveTrackerParams.Default, _BallTracker, _Calculator);
    }

    [Fact]
    public void Snapshot_exposes_observed_facts_and_all_candidates()
    {
        Assert.Null(_Testee.Latest);
        Frame frame = At(1000);
        TrackedBall predicted = Ball(frame, TrackingStatus.Predicted);
        TrackedBall observed = Ball(frame, TrackingStatus.Observed) with
        {
            Position = new Point(250, 300),
            Confidence = TrackingConfidence.High,
            VelocityPxPerS = new Vector2(20, 30),
        };
        Arrange(frame, predicted, observed);

        LiveTrackingSnapshot snapshot = _Testee.ApplyObservations(frame, []);

        Assert.Same(snapshot, _Testee.Latest);
        Assert.Equal(LiveTrackingPhase.Live, snapshot.Phase);
        Assert.True(snapshot.IsBallFound);
        Assert.Equal(frame, snapshot.Frame);
        Assert.Equal(observed.Position, snapshot.BallPosition);
        Assert.Equal(observed.VelocityPxPerS, snapshot.BallVelocityPxPerS);
        Assert.Equal(new[] { predicted, observed }, snapshot.BallCandidates);
        Assert.Equal(_Possession, snapshot.Possession);
        Assert.Equal(_Possession, snapshot.LastKnownPossession);
        Assert.Equal(BarType.A3, snapshot.ClosestBar);
        Assert.Equal(0, snapshot.PossessionTimeMs);
        Assert.Equal(0, snapshot.LastObservedBallAgeMs);
    }

    [Fact]
    public void Long_possession_remains_a_tracking_fact_without_game_decisions()
    {
        Observe(1000);

        LiveTrackingSnapshot snapshot = Observe(17001);

        Assert.Equal(LiveTrackingPhase.Live, snapshot.Phase);
        Assert.Equal(16001, snapshot.PossessionTimeMs);
        Assert.Equal(_Possession, snapshot.Possession);
    }

    [Fact]
    public void No_observation_since_start_is_lost_without_possession_or_age()
    {
        LiveTrackingSnapshot snapshot = Lose(3000);

        Assert.Equal(LiveTrackingPhase.BallLost, snapshot.Phase);
        Assert.False(snapshot.IsBallFound);
        Assert.Equal(BallPossession.None, snapshot.Possession);
        Assert.Equal(BallPossession.None, snapshot.LastKnownPossession);
        Assert.Equal(0, snapshot.LastObservedBallAgeMs);
        Assert.Equal(0, snapshot.PossessionTimeMs);
        Assert.Null(snapshot.ClosestBar);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Brief_loss_holds_possession_and_recovery_includes_missing_time(bool predicted)
    {
        Observe(1000);
        Frame missingFrame = At(1500);
        TrackedBall[] candidates = predicted ? [Ball(missingFrame, TrackingStatus.Predicted)] : [];
        Arrange(missingFrame, candidates);

        LiveTrackingSnapshot missing = _Testee.ApplyObservations(missingFrame, []);
        LiveTrackingSnapshot recovered = Observe(2000);

        Assert.Equal(LiveTrackingPhase.BallTemporarilyLost, missing.Phase);
        Assert.False(missing.IsBallFound);
        Assert.Equal(_Possession, missing.Possession);
        Assert.Equal(500, missing.PossessionTimeMs);
        Assert.Equal(500, missing.LastObservedBallAgeMs);
        Assert.Null(missing.ClosestBar);
        Assert.Equal(candidates, missing.BallCandidates);
        Assert.Equal(1000, recovered.PossessionTimeMs);
        Assert.Equal(0, recovered.LastObservedBallAgeMs);
    }

    [Theory]
    [InlineData(2999, LiveTrackingPhase.BallTemporarilyLost, 1999, 2100)]
    [InlineData(3000, LiveTrackingPhase.BallLost, 0, 0)]
    [InlineData(3001, LiveTrackingPhase.BallLost, 0, 0)]
    public void Hold_boundary_releases_current_possession_but_keeps_last_known(
        long missingTimeMs, LiveTrackingPhase phase, int heldTimeMs, int recoveredTimeMs)
    {
        Observe(1000);

        LiveTrackingSnapshot missing = Lose(missingTimeMs);
        LiveTrackingSnapshot recovered = Observe(3100);

        Assert.Equal(phase, missing.Phase);
        Assert.Equal(heldTimeMs, missing.PossessionTimeMs);
        Assert.Equal(phase == LiveTrackingPhase.BallLost ? BallPossession.None : _Possession, missing.Possession);
        Assert.Equal(_Possession, missing.LastKnownPossession);
        Assert.Equal(recoveredTimeMs, recovered.PossessionTimeMs);
    }

    [Fact]
    public void Possession_change_and_unassigned_ball_reset_duration()
    {
        Observe(1000);
        BallPossession other = new(Team.B, PossessionArea.FiveBar);
        _Calculator.Compute(Arg.Any<Point>()).Returns(other);

        LiveTrackingSnapshot changed = Observe(2000);
        LiveTrackingSnapshot held = Observe(2500);
        _Calculator.Compute(Arg.Any<Point>()).Returns(BallPossession.None);
        LiveTrackingSnapshot unassigned = Observe(3000);
        LiveTrackingSnapshot missing = Lose(3500);

        Assert.Equal(other, changed.Possession);
        Assert.Equal(0, changed.PossessionTimeMs);
        Assert.Equal(500, held.PossessionTimeMs);
        Assert.Equal(BallPossession.None, unassigned.LastKnownPossession);
        Assert.Equal(0, unassigned.PossessionTimeMs);
        Assert.Equal(BallPossession.None, missing.Possession);
        Assert.Equal(0, missing.PossessionTimeMs);
    }

    [Fact]
    public void Table_update_reaches_both_components_and_preserves_possession()
    {
        Observe(1000);

        _Testee.UpdateTableConfig(TableConfig.Config);
        LiveTrackingSnapshot snapshot = Observe(1500);

        _BallTracker.Received(1).UpdateTableConfig(TableConfig.Config);
        _Calculator.Received(1).UpdateTableConfig(TableConfig.Config);
        Assert.Equal(500, snapshot.PossessionTimeMs);
    }

    [Fact]
    public void Observed_ball_without_closest_bar_still_provides_tracking_facts()
    {
        _Calculator.FindClosestBarType(Arg.Any<Point>()).Returns(Option<BarType>.None());

        LiveTrackingSnapshot snapshot = Observe(1000);

        Assert.True(snapshot.IsBallFound);
        Assert.Null(snapshot.ClosestBar);
        Assert.Equal(_Possession, snapshot.Possession);
    }

    private LiveTrackingSnapshot Observe(long timeMs)
    {
        Frame frame = At(timeMs);
        Arrange(frame, Ball(frame, TrackingStatus.Observed));
        return _Testee.ApplyObservations(frame, []);
    }

    private LiveTrackingSnapshot Lose(long timeMs)
    {
        Frame frame = At(timeMs);
        Arrange(frame);
        return _Testee.ApplyObservations(frame, []);
    }

    private void Arrange(Frame frame, params TrackedBall[] candidates)
    {
        _BallTracker.ApplyObservations(frame, Arg.Any<IEnumerable<ObservedBall>>())
            .Returns(new TrackingSnapshot(frame, candidates));
    }

    private static Frame At(long timeMs) => new((ulong)timeMs, timeMs * 1_000_000L);

    private static TrackedBall Ball(Frame frame, TrackingStatus status) =>
        new(1, frame, new Point(200, 300), TrackingConfidence.Average, status, default);
}
