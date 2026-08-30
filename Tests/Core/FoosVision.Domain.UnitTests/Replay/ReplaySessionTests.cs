// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.Replay;

public class ReplaySessionTests
{
    [Fact]
    public void Fixture()
    {
        ReplaySession testee = CreateSession();

        Assert.False(testee.IsActive);
        Assert.True(testee.CurrentReplayId.IsNone);
        Assert.Equal(0, testee.CompletedLoops);
        Assert.Equal(ReplaySession.DefaultRequiredCompletedLoops, testee.RequiredCompletedLoops);
    }

    [Fact]
    public void Start_activates_replay()
    {
        ReplaySession testee = CreateSession();
        ReplayId replayId = new(42, 1_000_000);

        ReplayTrackedFrame anchorFrame = testee.Start(replayId, Anchor(), TableConfig.Config);

        Assert.True(testee.IsActive);
        Assert.Equal(replayId, testee.CurrentReplayId.Value);
        Assert.Equal(0, testee.CompletedLoops);
        Assert.Equal(ReplayTrackedFrameStatus.Anchor, anchorFrame.Status);
        Assert.Equal(new Point(100, 200), anchorFrame.BallPosition);
    }

    [Fact]
    public void Start_replaces_active_replay()
    {
        ReplaySession testee = CreateSession();
        ReplayId firstReplay = new(42, 1_000_000);
        ReplayId replacementReplay = new(84, 2_000_000);

        testee.Start(firstReplay, Anchor(), TableConfig.Config);
        testee.CompleteLoop();

        testee.Start(replacementReplay, Anchor(new Point(300, 400)), TableConfig.Config);

        Assert.True(testee.IsActive);
        Assert.Equal(replacementReplay, testee.CurrentReplayId.Value);
        Assert.Equal(0, testee.CompletedLoops);
    }

    [Fact]
    public void Complete_loop_is_ignored_without_active_replay()
    {
        ReplaySession testee = CreateSession();

        testee.CompleteLoop();

        Assert.Equal(0, testee.CompletedLoops);
    }

    [Fact]
    public void Cannot_return_to_live_before_required_loops_are_completed()
    {
        ReplaySession testee = CreateSession(requiredCompletedLoops: 2);
        testee.Start(new ReplayId(42, 1_000_000), Anchor(), TableConfig.Config);
        testee.CompleteLoop();

        bool canReturnToLive = testee.CanReturnToLive(new Point(100, 200));

        Assert.False(canReturnToLive);
    }

    [Fact]
    public void Cannot_return_to_live_after_required_loops_without_live_ball()
    {
        ReplaySession testee = CreateSession();
        testee.Start(new ReplayId(42, 1_000_000), Anchor(), TableConfig.Config);
        testee.CompleteLoop();

        bool canReturnToLive = testee.CanReturnToLive(null);

        Assert.False(canReturnToLive);
        Assert.True(testee.HasCompletedRequiredLoops);
    }

    [Fact]
    public void Cannot_return_to_live_when_first_live_ball_after_required_loops_is_found()
    {
        ReplaySession testee = CreateSession();
        testee.Start(new ReplayId(42, 1_000_000), Anchor(), TableConfig.Config);
        testee.CompleteLoop();

        bool canReturnToLive = testee.CanReturnToLive(new Point(100, 200));

        Assert.False(canReturnToLive);
    }

    [Fact]
    public void Cannot_return_to_live_after_required_loops_when_live_ball_moves_less_than_ten_pixels()
    {
        ReplaySession testee = CreateSession();
        testee.Start(new ReplayId(42, 1_000_000), Anchor(), TableConfig.Config);
        testee.CompleteLoop();

        _ = testee.CanReturnToLive(new Point(100, 200));
        bool canReturnToLive = testee.CanReturnToLive(new Point(109, 200));

        Assert.False(canReturnToLive);
    }

    [Fact]
    public void Can_return_to_live_after_required_loops_when_live_ball_moves_ten_pixels()
    {
        ReplaySession testee = CreateSession();
        testee.Start(new ReplayId(42, 1_000_000), Anchor(), TableConfig.Config);
        testee.CompleteLoop();

        _ = testee.CanReturnToLive(new Point(100, 200));
        bool canReturnToLive = testee.CanReturnToLive(new Point(110, 200));

        Assert.True(canReturnToLive);
    }

    [Fact]
    public void Stop_resets_replay()
    {
        ReplaySession testee = CreateSession();
        testee.Start(new ReplayId(42, 1_000_000), Anchor(), TableConfig.Config);
        testee.CompleteLoop();

        testee.Stop();

        Assert.False(testee.IsActive);
        Assert.True(testee.CurrentReplayId.IsNone);
        Assert.Equal(0, testee.CompletedLoops);
    }

    [Theory]
    [InlineData(999_999_998, false)]
    [InlineData(999_999_999, false)]
    [InlineData(1_000_000_000, true)]
    public void Can_apply_observations_only_allows_frames_after_track_anchor(long timeNs, bool expected)
    {
        ReplaySession testee = CreateStartedSession(new Point(200, 200));

        bool canApply = testee.CanApplyObservations(new Frame(1, timeNs));

        Assert.Equal(expected, canApply);
    }

    [Fact]
    public void Apply_observations_rejects_frames_at_track_anchor()
    {
        ReplaySession testee = CreateStartedSession(new Point(200, 200));

        Assert.Throws<InvalidOperationException>(() =>
            testee.ApplyObservations(new Frame(1, 999_999_999), []));
    }

    [Fact]
    public void Apply_observations_uses_tracker()
    {
        ReplaySession testee = CreateStartedSession(new Point(200, 200));

        ReplayTrackedFrame tracked = testee.ApplyObservations(
            new Frame(1, 1_000_000_000),
            [Ball(500, 500, 0.4), Ball(202, 201, 0.6)]);

        Assert.Equal(ReplayTrackedFrameStatus.Tracked, tracked.Status);
        Assert.Equal(new Point(202, 201), tracked.BallPosition);
    }

    [Fact]
    public void Get_ball_search_region_uses_last_known_position()
    {
        ReplaySession testee = CreateStartedSession(new Point(200, 200));

        Assert.Equal(new Rectangle(8, 8, 384, 384), testee.GetBallSearchRegion());

        testee.ApplyObservations(
            new Frame(1, 1_000_000_000),
            [Ball(260, 250, 0.8)]);

        Assert.Equal(new Rectangle(68, 58, 384, 384), testee.GetBallSearchRegion());
    }

    [Fact]
    public void Empty_observations_keep_tracking_from_seed()
    {
        Point seed = new(200, 200);
        ReplaySession testee = CreateStartedSession(seed);

        ReplayTrackedFrame tracked = testee.ApplyObservations(new Frame(1, 1_000_000_000), []);

        Assert.Equal(ReplayTrackedFrameStatus.Predicted, tracked.Status);
        Assert.Equal(seed, tracked.BallPosition);
    }

    [Fact]
    public void Predicted_replay_frame_is_distinguishable_from_anchor_and_tracked_frames()
    {
        var frame = new Frame(1, 1_000_000_000);
        var predictedPosition = new Point(240, 200);
        ReplaySession testee = CreateSession(
            new ScriptedBallTracker(
                CreateTrackedBall(new Frame(40, 999_999_999), new Point(200, 200), TrackingStatus.Observed),
                CreateTrackedBall(frame, predictedPosition, TrackingStatus.Predicted)),
            new ReplayAnalyzer(TableImageScale.From(TableConfig.Config)));
        testee.Start(
            new ReplayId(42, 1_000_000),
            new ReplayTrackAnchor(new Frame(40, 999_999_999), new Point(200, 200)),
            TableConfig.Config);

        ReplayTrackedFrame tracked = testee.ApplyObservations(frame, []);

        Assert.Equal(ReplayTrackedFrameStatus.Predicted, tracked.Status);
        Assert.Equal(predictedPosition, tracked.BallPosition);
    }

    [Fact]
    public void Get_ball_search_region_uses_predicted_position()
    {
        var frame = new Frame(1, 1_000_000_000);
        ReplaySession testee = CreateSession(
            new ScriptedBallTracker(
                CreateTrackedBall(new Frame(40, 999_999_999), new Point(200, 200), TrackingStatus.Observed),
                CreateTrackedBall(frame, new Point(240, 210), TrackingStatus.Predicted)),
            new ReplayAnalyzer(TableImageScale.From(TableConfig.Config)));
        testee.Start(
            new ReplayId(42, 1_000_000),
            new ReplayTrackAnchor(new Frame(40, 999_999_999), new Point(200, 200)),
            TableConfig.Config);

        testee.ApplyObservations(frame, []);

        Assert.Equal(new Rectangle(48, 18, 384, 384), testee.GetBallSearchRegion());
    }

    [Fact]
    public void Get_analysis_uses_replay_analyzer()
    {
        RecordingReplayAnalyzer analyzer = new();
        ReplaySession testee = CreateStartedSession(new Point(200, 200), analyzer);

        testee.ApplyObservations(
            new Frame(1, 1_000_000_000),
            [Ball(260, 200, 0.8)]);

        ReplayAnalysis analysis = testee.GetAnalysis();

        Assert.Same(analyzer.Analysis, analysis);
        Assert.Equal(2, analyzer.TrackedFrames.Count);
        Assert.Equal(ReplayTrackedFrameStatus.Anchor, analyzer.TrackedFrames[0].Status);
        Assert.Equal(ReplayTrackedFrameStatus.Tracked, analyzer.TrackedFrames[^1].Status);
    }

    private static ReplaySession CreateSession(int requiredCompletedLoops = ReplaySession.DefaultRequiredCompletedLoops)
        => CreateSession(new ReplayAnalyzer(TableImageScale.From(TableConfig.Config)), requiredCompletedLoops);

    private static ReplaySession CreateSession(IReplayAnalyzer analyzer, int requiredCompletedLoops = ReplaySession.DefaultRequiredCompletedLoops)
        => new(
            new BallTracker(BallTrackerParams.Default, TableConfig.Config),
            analyzer,
            requiredCompletedLoops);

    private static ReplaySession CreateSession(IBallTracker ballTracker, IReplayAnalyzer analyzer)
        => new(ballTracker, analyzer);

    private static ReplaySession CreateStartedSession(Point seed)
        => CreateStartedSession(seed, new ReplayAnalyzer(TableImageScale.From(TableConfig.Config)));

    private static ReplaySession CreateStartedSession(Point seed, IReplayAnalyzer analyzer)
    {
        BallTrackerParams parameters = BallTrackerParams.Default with
        {
            LowPassAlphaDeltaXY = 1.0,
        };
        ReplaySession session = new(new BallTracker(parameters, TableConfig.Config), analyzer);
        session.Start(new ReplayId(42, 1_000_000), new ReplayTrackAnchor(new Frame(40, 999_999_999), seed), TableConfig.Config);
        return session;
    }

    private static ObservedBall Ball(double x, double y, double quality)
        => new(new Point(x, y), quality);

    private static ReplayTrackAnchor Anchor()
        => Anchor(new Point(100, 200));

    private static ReplayTrackAnchor Anchor(Point position)
        => new(new Frame(40, 1_900_000_000), position);

    private static TrackedBall CreateTrackedBall(Frame frame, Point position, TrackingStatus status)
        => new(1, frame, position, TrackingConfidence.High, status, Vector2.Zero);

    private sealed class RecordingReplayAnalyzer : IReplayAnalyzer
    {
        public ReplayAnalysis Analysis { get; } = new([]);

        public IReadOnlyList<ReplayTrackedFrame> TrackedFrames { get; private set; } = [];

        public ReplayAnalysis Analyze(IEnumerable<ReplayTrackedFrame> trackedFrames)
        {
            TrackedFrames = [.. trackedFrames];
            return Analysis;
        }
    }

    private sealed class ScriptedBallTracker : IBallTracker
    {
        private readonly Queue<TrackedBall> _TrackedBalls;

        public ScriptedBallTracker(params TrackedBall[] trackedBalls)
        {
            _TrackedBalls = new(trackedBalls);
        }

        public TrackingSnapshot? Latest { get; private set; }

        public TrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
        {
            if (!_TrackedBalls.TryDequeue(out TrackedBall? trackedBall))
            {
                Latest = new(frame, []);
                return Latest;
            }

            Latest = new(frame, [trackedBall]);
            return Latest;
        }

        public void UpdateTableConfig(TableConfiguration tableConfig)
        {
        }
    }
}
