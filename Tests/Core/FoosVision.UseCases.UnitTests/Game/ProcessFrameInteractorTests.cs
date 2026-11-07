// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Services.GameTracking;
using FoosVision.Domain.Game.Services.ReplayDecision;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.UseCases.Game.ProcessFrame;
using FoosVision.UseCases.Live.Ports;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Game;

public class ProcessFrameInteractorTests
{
    private const long _1s = 1000 * 1_000_000L;

    private readonly FakeGameSessionStore _FakeStore;
    private readonly IProcessFrameOutputPort _Output;
    private readonly List<ProcessFrameResponse> _Responses = [];
    private readonly IFrameVisionOps _Vision;

    private readonly ProcessFrameInteractor _Testee;

    public ProcessFrameInteractorTests()
    {
        _FakeStore = new();
        _Output = Substitute.For<IProcessFrameOutputPort>();
        _Output.ReportProcessed(Arg.Any<ProcessFrameResponse>())
               .Returns(ci =>
               {
                   _Responses.Add(ci.Arg<ProcessFrameResponse>());
                   return Task.CompletedTask;
               });
        _Vision = Substitute.For<IFrameVisionOps>();
        _Vision.DetectBalls(Arg.Any<TableConfiguration>()).Returns([]);

        _Testee = new ProcessFrameInteractor(_FakeStore);
    }

    [Theory]
    [InlineData(5, false)]
    [InlineData(6, true)]
    public async Task Reports_return_only_after_recorder_wait(int delaySeconds, bool shouldReturn)
    {
        Frame trigger = new(1, _1s);
        Frame reference = new(2, (1 + delaySeconds) * _1s);
        Frame movement = new(3, reference.TimestampNs + 33_000_000);
        ReplayAnchor anchor = new(trigger, new(100, 200), BallPossession.None, 0, ReplayTriggerKind.BallDisappeared);
        _FakeStore.GameTracker.ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>()).Returns(
            CreateReplaySuggestedSnapshot(trigger, anchor),
            CreateSnapshot(reference, new(1, reference, new(100, 200), TrackingConfidence.High, TrackingStatus.Observed, Vector2.Zero)),
            CreateSnapshot(movement, new(1, movement, new(110, 200), TrackingConfidence.High, TrackingStatus.Observed, Vector2.Zero)));

        await _Testee.Handle(new(trigger, _Vision), _Output, CancellationToken.None);
        await _Testee.Handle(new(reference, _Vision), _Output, CancellationToken.None);
        await _Testee.Handle(new(movement, _Vision), _Output, CancellationToken.None);

        Assert.All(_Responses, response => Assert.Equal(_FakeStore.LoadActive().Value.Id, response.SessionId));
        Assert.Null(_Responses[0].ReturnToLiveReplayFrame);
        Assert.Null(_Responses[1].ReturnToLiveReplayFrame);
        Assert.Equal(shouldReturn ? trigger : (Frame?)null, _Responses[2].ReturnToLiveReplayFrame);
    }

    [Fact]
    public async Task Skips_when_no_active_game_session()
    {
        _FakeStore.Clear();
        var request = new ProcessFrameRequest(default, _Vision);

        await _Testee.Handle(request, _Output, CancellationToken.None);

        await _Output.Received().ReportSkipped(Arg.Any<string>());
        await _Output.DidNotReceiveWithAnyArgs().ReportProcessed(default!);
        _Vision.DidNotReceive().DetectBalls(Arg.Any<TableConfiguration>());
    }

    [Fact]
    public async Task Report_tracked_ball_info()
    {
        Frame frame = new(1, _1s);
        TrackedBall tracked = new(1, frame, new Point(1, 2), TrackingConfidence.Average, TrackingStatus.Observed, new Vector2(3, 4));
        TrackedBall candidate = new(1, frame, new Point(5, 6), TrackingConfidence.Low, TrackingStatus.Observed, Vector2.Zero);
        BallPossession possession = new(Team.A, PossessionArea.Defense);
        GameTrackingSnapshot snapshot = CreateSnapshot(frame, tracked, [candidate], possession);
        _FakeStore.GameTracker
            .ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>())
            .Returns(snapshot);

        var request = new ProcessFrameRequest(frame, _Vision);

        await _Testee.Handle(request, _Output, CancellationToken.None);

        Assert.Single(_Responses);
        var r = _Responses.Single();

        Assert.Equal(frame, r.Frame);
        Assert.True(r.IsBallFound);
        Assert.Equal(tracked.Position, r.BallPosition);
        Assert.Equal(tracked.VelocityPxPerS, r.BallVelocityPxPerS);
        Assert.Equal(possession, r.Possession);
        Assert.Equal(0, r.PossessionTimeMs);
        Assert.False(r.IsTimeFoul);
        Assert.False(r.RequestTableConfigUpdate);
        Assert.False(r.RequestTableSceneUpdate);
        Assert.Null(r.Replay);

        var ballCandidate = Assert.Single(r.BallCandidates);
        Assert.Equal(candidate.Position.X, ballCandidate.Position.X);
        Assert.Equal(candidate.Position.Y, ballCandidate.Position.Y);
        Assert.Equal(candidate.Status, ballCandidate.Status);
        Assert.Empty(r.Observations);

        _FakeStore.GameTracker.Received().ApplyObservations(frame, Arg.Any<IEnumerable<ObservedBall>>());
        _Vision.Received().DetectBalls(Arg.Any<TableConfiguration>());
    }

    [Fact]
    public async Task Reports_raw_observations_separately_from_tracked_candidates()
    {
        Frame frame = new(1, _1s);
        TrackedBall tracked = new(1, frame, new Point(1, 2), TrackingConfidence.Average, TrackingStatus.Observed, Vector2.Zero);
        ObservedBall observation = new(new Point(7, 8), 0.75);
        GameTrackingSnapshot snapshot = CreateSnapshot(frame, tracked);
        _FakeStore.GameTracker
            .ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>())
            .Returns(snapshot);
        _Vision.DetectBalls(Arg.Any<TableConfiguration>()).Returns([observation]);

        await _Testee.Handle(new ProcessFrameRequest(frame, _Vision), _Output, CancellationToken.None);

        ProcessFrameResponse response = Assert.Single(_Responses);
        ObservedBall reportedObservation = Assert.Single(response.Observations);
        Assert.Equal(observation, reportedObservation);
    }

    [Fact]
    public async Task Request_table_config_update_on_high_confidence_ball()
    {
        Frame frame = new(1, 10 * _1s);
        TrackedBall tracked = new(1, frame, new Point(100, 200), TrackingConfidence.Low, TrackingStatus.Observed, new Vector2(3, 4));
        GameTrackingSnapshot snapshot = CreateSnapshot(frame, tracked);
        _FakeStore.GameTracker
             .ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>())
             .Returns(snapshot);

        var request = new ProcessFrameRequest(frame, _Vision);

        await _Testee.Handle(request, _Output, CancellationToken.None);

        var r = Assert.Single(_Responses);
        Assert.True(r.RequestTableConfigUpdate);
    }

    [Fact]
    public async Task Request_table_scene_update_when_due()
    {
        Frame frame = new(1, _1s);
        TrackedBall tracked = new(1, frame, new Point(100, 200), TrackingConfidence.High, TrackingStatus.Observed, new Vector2(3, 4));
        GameTrackingSnapshot snapshot = CreateSnapshot(frame, tracked);
        _FakeStore.GameTracker
             .ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>())
             .Returns(snapshot);

        var request = new ProcessFrameRequest(frame, _Vision);

        await _Testee.Handle(request, _Output, CancellationToken.None);

        var r = Assert.Single(_Responses);
        Assert.True(r.RequestTableSceneUpdate);
    }

    [Fact]
    public async Task Request_potential_goal_check_after_ball_lost()
    {
        Frame frame1 = new(1, 1 * _1s);
        TrackedBall tracked1 = new(1, frame1, new Point(100, 200), TrackingConfidence.High, TrackingStatus.Observed, new Vector2(3, 4));
        BallPossession possession = new(Team.A, PossessionArea.ThreeBar);
        GameTrackingSnapshot snapshot1 = CreateSnapshot(frame1, tracked1, possession: possession);
        Frame frame2 = new(1, 3 * _1s);
        ReplayAnchor anchor = new(frame1, tracked1.Position, possession, 1234, ReplayTriggerKind.BallDisappeared);
        GameTrackingSnapshot snapshot2 = CreateReplaySuggestedSnapshot(frame2, anchor);
        _FakeStore.GameTracker
            .ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>())
            .Returns(snapshot1, snapshot2);

        var req1 = new ProcessFrameRequest(frame1, _Vision);
        var req2 = new ProcessFrameRequest(frame2, _Vision);

        await _Testee.Handle(req1, _Output, CancellationToken.None);
        await _Testee.Handle(req2, _Output, CancellationToken.None);

        Assert.Equal(2, _Responses.Count);
        var r = _Responses.Last();

        Assert.False(r.IsBallFound);
        Assert.Equal(0, r.PossessionTimeMs);
        Assert.False(r.IsTimeFoul);
        Assert.NotNull(r.Replay);
        Assert.Equal(frame1, r.Replay.AnchorFrame);
        Assert.Equal(tracked1.Position, r.Replay.AnchorPosition);
        Assert.Equal(possession, r.Replay.AnchorPossession);
        Assert.Equal(1234, r.Replay.AnchorPossessionTimeMs);
        Assert.Equal(frame1.TimestampNs, r.Replay.StartTimestampNs);
        Assert.Equal(frame1.TimestampNs + 1_000_000_000, r.Replay.EndTimestampNs);
    }

    [Fact]
    public async Task Report_possession_time_and_time_foul()
    {
        Frame frame1 = new(1, 1 * _1s);
        Frame frame2 = new(2, 17 * _1s);
        TrackedBall tracked1 = new(1, frame1, new Point(100, 200), TrackingConfidence.High, TrackingStatus.Observed, new Vector2(3, 4));
        TrackedBall tracked2 = new(1, frame2, new Point(101, 201), TrackingConfidence.High, TrackingStatus.Observed, new Vector2(3, 4));
        BallPossession possession = new(Team.A, PossessionArea.Defense);
        GameTrackingSnapshot snapshot1 = CreateSnapshot(frame1, tracked1, possession: possession);
        GameTrackingSnapshot snapshot2 = CreateSnapshot(frame2, tracked2, possession: possession, possessionTimeMs: 16000, isTimeFoul: true);
        _FakeStore.GameTracker
            .ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>())
            .Returns(snapshot1, snapshot2);

        await _Testee.Handle(new ProcessFrameRequest(frame1, _Vision), _Output, CancellationToken.None);
        await _Testee.Handle(new ProcessFrameRequest(frame2, _Vision), _Output, CancellationToken.None);

        ProcessFrameResponse r = _Responses.Last();
        Assert.Equal(16000, r.PossessionTimeMs);
        Assert.True(r.IsTimeFoul);
    }

    private static GameTrackingSnapshot CreateSnapshot(
        Frame frame,
        TrackedBall trackedBall,
        IEnumerable<TrackedBall>? otherCandidates = null,
        BallPossession? possession = null,
        int possessionTimeMs = 0,
        bool isTimeFoul = false)
    {
        List<TrackedBall> candidates = [trackedBall];

        if (otherCandidates != null)
        {
            candidates.AddRange(otherCandidates);
        }

        var currentPossession = possession ?? BallPossession.None;

        return new(
            frame,
            GameTrackingPhase.Live,
            true,
            trackedBall.Position,
            trackedBall.VelocityPxPerS,
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
}
