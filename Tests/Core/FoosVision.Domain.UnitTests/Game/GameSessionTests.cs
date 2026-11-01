// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Entities;
using FoosVision.Domain.Game.Services.GameTracking;
using FoosVision.Domain.Game.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;
using NSubstitute;

namespace FoosVision.Domain.UnitTests.Game;

public class GameSessionTests
{
    private const long _1s = 1000 * 1_000_000L;

    private static readonly BallPossession _AnyPossession = new(Team.A, PossessionArea.Defense);
    private readonly IGameTracker _GameTracker;
    private readonly GameSession _Testee;

    public GameSessionTests()
    {
        _GameTracker = Substitute.For<IGameTracker>();
        _Testee = new(Guid.Empty, _GameTracker, TableConfig.Config);
    }

    [Fact]
    public void Ball_is_tracked()
    {
        Frame frame = CreateFrame(1, 1);
        TrackedBall tracked = CreateTrackedBall(frame, 1, 100, 100, TrackingConfidence.Average, 10, 5);
        TrackedBall candidate = CreateTrackedBall(frame, 2, 120, 98, TrackingConfidence.Low);
        ArrangeSnapshots(CreateSnapshot(frame, tracked, [candidate]));

        var changes = _Testee.ApplyObservations(frame, []);

        Assert.Single(changes);
        var info = Assert.IsType<TrackedBallInfo>(changes.Single());
        Assert.Equal(_AnyPossession, info.Possession);
        Assert.Equal(0, info.PossessionTimeMs);
        Assert.False(info.IsTimeFoul);
        Assert.True(info.IsFound);
        Assert.Equal(tracked.Position, info.Position);
        Assert.Equal(tracked.VelocityPxPerS, info.VelocityPxPerS);
        Assert.Single(info.Candidates);
        var c = info.Candidates.First();
        Assert.Equal(candidate.Position.X, c.Position.X);
        Assert.Equal(candidate.Position.Y, c.Position.Y);
        Assert.Equal(candidate.Status, c.Status);
    }

    [Fact]
    public void Recorder_waits_six_seconds_then_requests_return_after_ten_pixels_of_new_movement()
    {
        Frame trigger = CreateFrame(1, 1);
        Frame reference = CreateFrame(3, 7);
        Frame near = CreateFrame(4, 8);
        Frame movement = CreateFrame(5, 9);
        Frame lost = CreateFrame(6, 10);
        ReplayAnchor anchor = new(trigger, new(100, 200), _AnyPossession, 1234, ReplayTriggerKind.BallDisappeared);
        ArrangeSnapshots(
            CreateReplaySuggestedSnapshot(trigger, anchor),
            CreateSnapshot(CreateFrame(2, 2), null),
            CreateObservedSnapshot(reference, 100),
            CreateObservedSnapshot(near, 109),
            CreateObservedSnapshot(movement, 110),
            CreateSnapshot(lost, null));

        Assert.Contains(_Testee.ApplyObservations(trigger, []), c => c is ReplayRequest);
        Assert.Empty(_Testee.ApplyObservations(CreateFrame(2, 2), []).OfType<ReturnToLiveRequest>());
        Assert.Empty(_Testee.ApplyObservations(reference, []).OfType<ReturnToLiveRequest>());
        Assert.Empty(_Testee.ApplyObservations(near, []).OfType<ReturnToLiveRequest>());
        Assert.Equal(trigger, Assert.Single(_Testee.ApplyObservations(movement, []).OfType<ReturnToLiveRequest>()).ReplayTriggerFrame);
        Assert.Equal(trigger, Assert.Single(_Testee.ApplyObservations(lost, []).OfType<ReturnToLiveRequest>()).ReplayTriggerFrame);
    }

    [Fact]
    public void New_replay_restarts_wait_and_resets_previous_reference_and_return_decision()
    {
        Frame first = CreateFrame(1, 1);
        Frame second = CreateFrame(4, 9);
        ReplayAnchor anchor = new(first, new(100, 200), _AnyPossession, 0, ReplayTriggerKind.SavedShot);
        ArrangeSnapshots(
            CreateReplaySuggestedSnapshot(first, anchor),
            CreateObservedSnapshot(CreateFrame(2, 7), 100),
            CreateObservedSnapshot(CreateFrame(3, 8), 110),
            CreateReplaySuggestedSnapshot(second, anchor),
            CreateObservedSnapshot(CreateFrame(5, 10), 500),
            CreateObservedSnapshot(CreateFrame(6, 15), 500),
            CreateObservedSnapshot(CreateFrame(7, 16), 510));
        _Testee.ApplyObservations(first, []);
        _Testee.ApplyObservations(CreateFrame(2, 7), []);
        Assert.Single(_Testee.ApplyObservations(CreateFrame(3, 8), []).OfType<ReturnToLiveRequest>());
        Assert.Empty(_Testee.ApplyObservations(second, []).OfType<ReturnToLiveRequest>());
        Assert.Empty(_Testee.ApplyObservations(CreateFrame(5, 10), []).OfType<ReturnToLiveRequest>());
        Assert.Empty(_Testee.ApplyObservations(CreateFrame(6, 15), []).OfType<ReturnToLiveRequest>());
        Assert.Equal(second, Assert.Single(_Testee.ApplyObservations(CreateFrame(7, 16), []).OfType<ReturnToLiveRequest>()).ReplayTriggerFrame);
    }

    [Fact]
    public void Shot_followthrough_is_ignored_and_stationary_ball_waits_for_new_movement_after_delay()
    {
        Frame trigger = CreateFrame(1, 1);
        ReplayAnchor anchor = new(trigger, new(100, 200), _AnyPossession, 0, ReplayTriggerKind.SavedShot);
        Frame[] frames = [trigger, CreateFrame(2, 2), CreateFrame(3, 3), CreateFrame(4, 6),
            CreateFrame(5, 7), CreateFrame(6, 8), CreateFrame(7, 9), CreateFrame(8, 10)];
        double[] positions = [100, 140, 160, 160, 160, 169, 170];
        ArrangeSnapshots([
            CreateReplaySuggestedSnapshot(trigger, anchor),
            .. positions.Select((x, index) => CreateObservedSnapshot(frames[index + 1], x)),
        ]);
        foreach (Frame frame in frames[..^1])
        {
            Assert.Empty(_Testee.ApplyObservations(frame, []).OfType<ReturnToLiveRequest>());
        }

        Assert.Equal(trigger, Assert.Single(_Testee.ApplyObservations(frames[^1], []).OfType<ReturnToLiveRequest>()).ReplayTriggerFrame);
    }

    [Theory]
    [InlineData(5_999_999_999, false)]
    [InlineData(6_000_000_000, true)]
    public void Movement_reference_starts_at_exact_recorder_frame_deadline(long elapsedNs, bool shouldReturn)
    {
        Frame trigger = CreateFrame(1, 1);
        Frame reference = new(2, trigger.TimestampNs + elapsedNs);
        Frame movement = new(3, reference.TimestampNs + 1);
        ReplayAnchor anchor = new(trigger, new(100, 200), _AnyPossession, 0, ReplayTriggerKind.SavedShot);
        ArrangeSnapshots(CreateReplaySuggestedSnapshot(trigger, anchor), CreateObservedSnapshot(reference, 100), CreateObservedSnapshot(movement, 110));
        _Testee.ApplyObservations(trigger, []);
        Assert.Empty(_Testee.ApplyObservations(reference, []).OfType<ReturnToLiveRequest>());
        Assert.Equal(shouldReturn, _Testee.ApplyObservations(movement, []).OfType<ReturnToLiveRequest>().Any());
    }

    [Fact]
    public void Ball_is_tracked_and_table_config_update_requested()
    {
        Frame frame10 = CreateFrame(1, 10);
        TrackedBall tracked = CreateTrackedBall(frame10, 1, 100, 0, TrackingConfidence.Low);
        ArrangeSnapshots(CreateSnapshot(frame10, tracked));

        var changes = _Testee.ApplyObservations(frame10, []).ToList();

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c is TrackedBallInfo);
        Assert.Contains(changes, c => c is UpdateTableConfigRequest);
    }

    [Fact]
    public void Table_config_update_is_not_requested_while_calibration_update_is_in_progress()
    {
        Frame frame10 = CreateFrame(1, 10);
        Frame frame20 = CreateFrame(2, 20);
        Frame frame30 = CreateFrame(3, 30);
        ArrangeSnapshots(
            CreateSnapshot(frame10, CreateTrackedBall(frame10, 1, 100, 0, TrackingConfidence.Low)),
            CreateSnapshot(frame20, CreateTrackedBall(frame20, 1, 100, 0, TrackingConfidence.Low)),
            CreateSnapshot(frame30, CreateTrackedBall(frame30, 1, 100, 0, TrackingConfidence.Low)));

        _ = _Testee.ApplyObservations(frame10, []).ToList();
        IReadOnlyList<Change> changesWhileInProgress = _Testee.ApplyObservations(frame20, []);

        _Testee.CompleteTableUpdate();
        IReadOnlyList<Change> changesAfterCompletion = _Testee.ApplyObservations(frame30, []);

        Assert.DoesNotContain(changesWhileInProgress, c => c is UpdateTableConfigRequest);
        Assert.Contains(changesAfterCompletion, c => c is UpdateTableConfigRequest);
    }

    [Fact]
    public void Ball_is_tracked_and_table_scene_update_requested()
    {
        Frame frame = CreateFrame(1, 1);
        TrackedBall tracked = CreateTrackedBall(frame, 1, 100, 0, TrackingConfidence.High);
        ArrangeSnapshots(CreateSnapshot(frame, tracked));

        var changes = _Testee.ApplyObservations(frame, []).ToList();

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c is TrackedBallInfo);
        Assert.Contains(changes, c => c is UpdateTableSceneRequest);
    }

    [Fact]
    public void Table_scene_update_is_not_requested_while_calibration_update_is_in_progress()
    {
        Frame frame1 = CreateFrame(1, 1);
        Frame frame2 = CreateFrame(2, 2);
        Frame frame3 = CreateFrame(3, 3);
        ArrangeSnapshots(
            CreateSnapshot(frame1, CreateTrackedBall(frame1, 1, 100, 0, TrackingConfidence.High)),
            CreateSnapshot(frame2, CreateTrackedBall(frame2, 1, 200, 0, TrackingConfidence.High)),
            CreateSnapshot(frame3, CreateTrackedBall(frame3, 1, 300, 0, TrackingConfidence.High)));

        _ = _Testee.ApplyObservations(frame1, []).ToList();
        IReadOnlyList<Change> changesWhileInProgress = _Testee.ApplyObservations(frame2, []);

        _Testee.CompleteTableSceneUpdate();
        IReadOnlyList<Change> changesAfterCompletion = _Testee.ApplyObservations(frame3, []);

        Assert.DoesNotContain(changesWhileInProgress, c => c is UpdateTableSceneRequest);
        Assert.Contains(changesAfterCompletion, c => c is UpdateTableSceneRequest);
    }

    [Fact]
    public void Table_scene_update_is_not_requested_when_table_update_was_requested_for_same_frame()
    {
        Frame frame = CreateFrame(1, 10);
        TrackedBall tracked = CreateTrackedBall(frame, 1, 100, 0, TrackingConfidence.High);
        ArrangeSnapshots(CreateSnapshot(frame, tracked));

        var changes = _Testee.ApplyObservations(frame, []).ToList();

        Assert.Contains(changes, c => c is UpdateTableConfigRequest);
        Assert.DoesNotContain(changes, c => c is UpdateTableSceneRequest);
    }

    [Fact]
    public void Update_table_config_delegates_to_game_tracker()
    {
        _Testee.UpdateTableConfig(TableConfig.Config);

        _GameTracker.Received().UpdateTableConfig(TableConfig.Config);
        Assert.Equal(TableConfig.Config, _Testee.TableConfig);
    }

    [Fact]
    public void Ball_is_lost()
    {
        Frame frame1 = CreateFrame(1, 1);
        TrackedBall tracked = CreateTrackedBall(frame1, 1, 100, 0, TrackingConfidence.High);
        ArrangeSnapshots(CreateSnapshot(frame1, tracked));

        _ = _Testee.ApplyObservations(frame1, []).ToList();

        Frame frame2 = CreateFrame(1, 2);
        ArrangeSnapshots(CreateSnapshot(frame2, null));

        var changes = _Testee.ApplyObservations(frame2, []).ToList();

        Assert.Single(changes);
        var info = Assert.IsType<TrackedBallInfo>(changes.Single());
        Assert.Equal(BallPossession.None, info.Possession);
        Assert.Equal(0, info.PossessionTimeMs);
        Assert.False(info.IsTimeFoul);
        Assert.False(info.IsFound);
        Assert.Empty(info.Candidates);
    }

    [Fact]
    public void Ball_is_lost_and_check_for_goal_requested()
    {
        Frame frame1 = CreateFrame(1, 1);
        TrackedBall tracked = CreateTrackedBall(frame1, 1, 100, 100, TrackingConfidence.High);
        Frame frame3 = CreateFrame(1, 3);
        ReplayAnchor anchor = new(frame1, tracked.Position, _AnyPossession, 1234, ReplayTriggerKind.BallDisappeared);
        ArrangeSnapshots(
            CreateSnapshot(frame1, tracked),
            CreateReplaySuggestedSnapshot(frame3, anchor));

        _ = _Testee.ApplyObservations(frame1, []).ToList();
        var changes = _Testee.ApplyObservations(frame3, []).ToList();

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, c => c is TrackedBallInfo);
        var requestInfo = changes.OfType<ReplayRequest>().FirstOrDefault();
        Assert.NotNull(requestInfo);
        Assert.Equal(frame1, requestInfo.AnchorFrame);
        Assert.Equal(tracked.Position, requestInfo.AnchorPosition);
        Assert.Equal(_AnyPossession, requestInfo.AnchorPossession);
        Assert.Equal(1234, requestInfo.AnchorPossessionTimeMs);
        Assert.Equal(ReplayTriggerKind.BallDisappeared, requestInfo.TriggerKind);
    }

    [Fact]
    public void Ball_is_lost_and_check_for_goal_is_skipped_without_anchor()
    {
        Frame frame1 = CreateFrame(1, 1);
        TrackedBall tracked = CreateTrackedBall(frame1, 1, 100, 100, TrackingConfidence.High);
        Frame frame3 = CreateFrame(1, 3);
        ArrangeSnapshots(
            CreateSnapshot(frame1, tracked),
            CreateSnapshot(frame3, null));

        _ = _Testee.ApplyObservations(frame1, []).ToList();
        var changes = _Testee.ApplyObservations(frame3, []).ToList();

        Assert.Single(changes);
        Assert.Contains(changes, c => c is TrackedBallInfo);
    }

    [Fact]
    public void Apply_observations_delegates_to_game_tracker()
    {
        Frame frame = CreateFrame(1, 1);
        TrackedBall tracked = CreateTrackedBall(frame, 1, 100, 100, TrackingConfidence.Average);
        ArrangeSnapshots(CreateSnapshot(frame, tracked));

        _ = _Testee.ApplyObservations(frame, []).ToList();

        _GameTracker.Received().ApplyObservations(frame, Arg.Any<IEnumerable<ObservedBall>>());
    }

    [Fact]
    public void Possession_time_accumulates_while_ball_stays_in_same_area()
    {
        Frame frame1 = CreateFrame(1, 1);
        Frame frame2 = CreateFrame(2, 4);
        TrackedBall tracked1 = CreateTrackedBall(frame1, 1, 100, 100, TrackingConfidence.High);
        TrackedBall tracked2 = CreateTrackedBall(frame2, 1, 102, 100, TrackingConfidence.High);
        ArrangeSnapshots(
            CreateSnapshot(frame1, tracked1),
            CreateSnapshot(frame2, tracked2, possessionTimeMs: 3000));

        _ = _Testee.ApplyObservations(frame1, []);
        IReadOnlyList<Change> changes = _Testee.ApplyObservations(frame2, []);

        TrackedBallInfo info = GetTrackedBallInfo(changes);
        Assert.Equal(3000, info.PossessionTimeMs);
        Assert.False(info.IsTimeFoul);
    }

    [Fact]
    public void Possession_time_resets_when_area_changes()
    {
        Frame frame1 = CreateFrame(1, 1);
        Frame frame2 = CreateFrame(2, 4);
        BallPossession nextPossession = new(Team.A, PossessionArea.FiveBar);
        TrackedBall tracked1 = CreateTrackedBall(frame1, 1, 100, 100, TrackingConfidence.High);
        TrackedBall tracked2 = CreateTrackedBall(frame2, 1, 400, 100, TrackingConfidence.High);
        ArrangeSnapshots(
            CreateSnapshot(frame1, tracked1),
            CreateSnapshot(frame2, tracked2, possession: nextPossession));

        _ = _Testee.ApplyObservations(frame1, []);
        IReadOnlyList<Change> changes = _Testee.ApplyObservations(frame2, []);

        TrackedBallInfo info = GetTrackedBallInfo(changes);
        Assert.Equal(nextPossession, info.Possession);
        Assert.Equal(0, info.PossessionTimeMs);
        Assert.False(info.IsTimeFoul);
    }

    [Fact]
    public void Time_foul_is_true_for_defense_after_more_than_15_seconds()
    {
        Frame frame1 = CreateFrame(1, 1);
        Frame frame2 = CreateFrame(2, 17);
        TrackedBall tracked1 = CreateTrackedBall(frame1, 1, 100, 100, TrackingConfidence.High);
        TrackedBall tracked2 = CreateTrackedBall(frame2, 1, 110, 100, TrackingConfidence.High);
        ArrangeSnapshots(
            CreateSnapshot(frame1, tracked1),
            CreateSnapshot(frame2, tracked2, possessionTimeMs: 16000, isTimeFoul: true));

        _ = _Testee.ApplyObservations(frame1, []);
        IReadOnlyList<Change> changes = _Testee.ApplyObservations(frame2, []);

        TrackedBallInfo info = GetTrackedBallInfo(changes);
        Assert.Equal(16000, info.PossessionTimeMs);
        Assert.True(info.IsTimeFoul);
    }

    [Fact]
    public void Time_foul_is_true_for_five_bar_after_more_than_10_seconds()
    {
        BallPossession fiveBarPossession = new(Team.A, PossessionArea.FiveBar);
        Frame frame1 = CreateFrame(1, 1);
        Frame frame2 = CreateFrame(2, 12);
        TrackedBall tracked1 = CreateTrackedBall(frame1, 1, 100, 100, TrackingConfidence.High);
        TrackedBall tracked2 = CreateTrackedBall(frame2, 1, 110, 100, TrackingConfidence.High);
        ArrangeSnapshots(
            CreateSnapshot(frame1, tracked1, possession: fiveBarPossession),
            CreateSnapshot(frame2, tracked2, possession: fiveBarPossession, possessionTimeMs: 11000, isTimeFoul: true));

        _ = _Testee.ApplyObservations(frame1, []);
        IReadOnlyList<Change> changes = _Testee.ApplyObservations(frame2, []);

        TrackedBallInfo info = GetTrackedBallInfo(changes);
        Assert.Equal(11000, info.PossessionTimeMs);
        Assert.True(info.IsTimeFoul);
    }

    private static GameTrackingSnapshot CreateObservedSnapshot(Frame frame, double x)
        => CreateSnapshot(frame, CreateTrackedBall(frame, 1, x, 200, TrackingConfidence.High));

    private void ArrangeSnapshots(params GameTrackingSnapshot[] snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        Assert.NotEmpty(snapshots);

        _GameTracker.ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>())
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
        TrackingConfidence confidence,
        double velocityX = 0,
        double velocityY = 0)
    {
        return new TrackedBall(
            id,
            frame,
            new Point(x, y),
            confidence,
            TrackingStatus.Observed,
            new Vector2(velocityX, velocityY));
    }

    private static GameTrackingSnapshot CreateSnapshot(
        Frame frame,
        TrackedBall? trackedBall,
        IEnumerable<TrackedBall>? otherCandidates = null,
        BallPossession? possession = null,
        int possessionTimeMs = 0,
        bool isTimeFoul = false)
    {
        List<TrackedBall> candidates = [];

        if (trackedBall != null)
        {
            candidates.Add(trackedBall);
        }

        if (otherCandidates != null)
        {
            candidates.AddRange(otherCandidates);
        }

        bool isFound = trackedBall?.Status == TrackingStatus.Observed;
        var ballPosition = trackedBall?.Position ?? default;
        var ballVelocity = trackedBall?.VelocityPxPerS ?? default;
        var currentPossession = isFound ? possession ?? _AnyPossession : BallPossession.None;

        return new(
            frame,
            isFound ? GameTrackingPhase.Live : GameTrackingPhase.BallLost,
            isFound,
            ballPosition,
            ballVelocity,
            candidates,
            currentPossession,
            currentPossession,
            possessionTimeMs,
            0,
            isTimeFoul,
            false,
            null);
    }

    private static GameTrackingSnapshot CreateReplaySuggestedSnapshot(Frame frame, ReplayAnchor anchor)
    {
        return new(
            frame,
            GameTrackingPhase.ReplaySuggested,
            false,
            default,
            default,
            [],
            BallPossession.None,
            anchor.Possession,
            0,
            2000,
            false,
            true,
            anchor);
    }

    private static TrackedBallInfo GetTrackedBallInfo(IEnumerable<Change> changes)
    {
        return Assert.IsType<TrackedBallInfo>(changes.Single(static c => c is TrackedBallInfo));
    }
}
