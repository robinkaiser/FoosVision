// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Game.Orchestration;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Recorder.UnitTests.Game.Orchestration;

public class ReplayCoordinatorTests
{
    private const long _1s = 1000L * 1_000_000L;
    private const long _700ms = 700L * 1_000_000L;

    [Fact]
    public async Task RequestReplay_publishes_replay_from_anchor_frame_for_one_second()
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new();
        FakeLiveVideoStreamController liveVideoStream = new();
        using ReplayCoordinator testee = new(replayBuffer, liveVideoStream, publisher);

        Frame triggerFrame = new(12, 5 * _1s);
        Frame anchorFrame = new(10, 3 * _1s);
        Point anchorPosition = new(100, 200);
        BallPossession anchorPossession = new(Team.A, PossessionArea.ThreeBar);
        const int anchorPossessionTimeMs = 9250;

        await testee.RequestReplay(
            triggerFrame,
            anchorFrame,
            anchorPosition,
            anchorPossession,
            anchorPossessionTimeMs,
            ReplayTriggerKind.BallDisappeared);
        ReplayMessage message = await publisher.WaitForReplay();

        Assert.Equal(triggerFrame.Id, message.TriggerFrameId);
        Assert.Equal(triggerFrame.TimestampNs, message.TriggerTimestampNs);
        Assert.Equal(anchorFrame.Id, message.AnchorFrameId);
        Assert.Equal(anchorFrame.TimestampNs, message.AnchorTimestampNs);
        Assert.Equal(anchorPosition.X, message.AnchorPosition.X);
        Assert.Equal(anchorPosition.Y, message.AnchorPosition.Y);
        Assert.Equal(TeamMessage.A, message.AnchorPossession.Team);
        Assert.Equal(PossessionAreaMessage.ThreeBar, message.AnchorPossession.Area);
        Assert.Equal(anchorPossessionTimeMs, message.AnchorPossessionTimeMs);
        Assert.Equal(anchorFrame.TimestampNs, message.ReplayStartTimestampNs);
        Assert.Equal(anchorFrame.TimestampNs + _1s, message.ReplayEndTimestampNs);
        Assert.Equal(EncodedReplayCodecMessage.H264, message.Codec);
        Assert.Single(message.ParameterSets);
        Assert.Single(message.AccessUnits);

        Assert.Equal(anchorFrame.TimestampNs, replayBuffer.RequestedStartTimeNs);
        Assert.Equal(anchorFrame.TimestampNs + _1s, replayBuffer.RequestedEndTimeNs);
        Assert.Equal(1, publisher.PublishReplayStartedCallCount);
        Assert.Equal(TeamMessage.A, publisher.LastReplayStarted?.AnchorPossession.Team);
        Assert.Equal(PossessionAreaMessage.ThreeBar, publisher.LastReplayStarted?.AnchorPossession.Area);
        Assert.Equal(anchorPossessionTimeMs, publisher.LastReplayStarted?.AnchorPossessionTimeMs);
        Assert.Equal(["pause", "resume"], liveVideoStream.Calls);
    }

    [Fact]
    public async Task RequestReplay_returns_without_waiting_for_publish()
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new() { BlockPublishing = true };
        using ReplayCoordinator testee = new(replayBuffer, new FakeLiveVideoStreamController(), publisher);

        Task request = testee.RequestReplay(
            new Frame(12, 5 * _1s),
            new Frame(10, 3 * _1s),
            new Point(100, 200),
            new BallPossession(Team.A, PossessionArea.ThreeBar),
            9250,
            ReplayTriggerKind.BallDisappeared);

        Assert.True(request.IsCompletedSuccessfully);
        await publisher.WaitUntilPublishStarted();

        publisher.AllowPublish();
    }

    [Fact]
    public async Task RequestReplay_drops_second_request_while_first_is_being_sent()
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new() { BlockPublishing = true };
        FakeLiveVideoStreamController liveVideoStream = new();
        using ReplayCoordinator testee = new(replayBuffer, liveVideoStream, publisher);

        await testee.RequestReplay(
            new Frame(12, 5 * _1s),
            new Frame(10, 3 * _1s),
            new Point(100, 200),
            new BallPossession(Team.A, PossessionArea.ThreeBar),
            9250,
            ReplayTriggerKind.BallDisappeared);
        await publisher.WaitUntilPublishStarted();

        await testee.RequestReplay(
            new Frame(22, 8 * _1s),
            new Frame(20, 6 * _1s),
            new Point(300, 400),
            new BallPossession(Team.B, PossessionArea.FiveBar),
            10_000,
            ReplayTriggerKind.BallDisappeared);

        publisher.AllowPublish();
        await publisher.WaitForReplay();
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(1, publisher.PublishReplayCallCount);
        Assert.Equal(["pause", "resume"], liveVideoStream.Calls);
    }

    [Fact]
    public async Task RequestReplay_accepts_second_request_after_first_was_sent()
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new();
        FakeLiveVideoStreamController liveVideoStream = new();
        using ReplayCoordinator testee = new(replayBuffer, liveVideoStream, publisher);

        await testee.RequestReplay(
            new Frame(12, 5 * _1s),
            new Frame(10, 3 * _1s),
            new Point(100, 200),
            new BallPossession(Team.A, PossessionArea.ThreeBar),
            9250,
            ReplayTriggerKind.BallDisappeared);
        ReplayMessage firstMessage = await publisher.WaitForReplay();

        await testee.RequestReplay(
            new Frame(22, 8 * _1s),
            new Frame(20, 6 * _1s),
            new Point(300, 400),
            new BallPossession(Team.B, PossessionArea.FiveBar),
            10_000,
            ReplayTriggerKind.BallDisappeared);
        ReplayMessage secondMessage = await publisher.WaitForReplay(2);

        Assert.Equal(12UL, firstMessage.TriggerFrameId);
        Assert.Equal(22UL, secondMessage.TriggerFrameId);
        Assert.Equal(2, publisher.PublishReplayCallCount);
        Assert.Equal(2, publisher.PublishReplayStartedCallCount);
        Assert.Equal(["pause", "resume", "pause", "resume"], liveVideoStream.Calls);
    }

    [Fact]
    public async Task RequestReplay_uses_seven_hundred_milliseconds_for_saved_shot()
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new();
        using ReplayCoordinator testee = new(replayBuffer, new FakeLiveVideoStreamController(), publisher);

        Frame triggerFrame = new(12, 5 * _1s);
        Frame anchorFrame = new(10, 3 * _1s);

        await testee.RequestReplay(
            triggerFrame,
            anchorFrame,
            new Point(100, 200),
            new BallPossession(Team.A, PossessionArea.ThreeBar),
            9250,
            ReplayTriggerKind.SavedShot);
        ReplayMessage message = await publisher.WaitForReplay();

        Assert.Equal(anchorFrame.TimestampNs, message.ReplayStartTimestampNs);
        Assert.Equal(anchorFrame.TimestampNs + _700ms, message.ReplayEndTimestampNs);
        Assert.Equal(anchorFrame.TimestampNs, replayBuffer.RequestedStartTimeNs);
        Assert.Equal(anchorFrame.TimestampNs + _700ms, replayBuffer.RequestedEndTimeNs);
    }

    private class FakeReplayBuffer : IEncodedReplayBuffer
    {
        public long RequestedStartTimeNs { get; private set; }

        public long RequestedEndTimeNs { get; private set; }

        public bool TryGetReplaySegment(long startTimeNs, long endTimeNs, out EncodedReplaySegment segment)
        {
            RequestedStartTimeNs = startTimeNs;
            RequestedEndTimeNs = endTimeNs;
            segment = new EncodedReplaySegment(
                EncodedReplayCodec.H264,
                startTimeNs,
                endTimeNs,
                [new EncodedReplayParameterSet(EncodedReplayParameterSetType.SPS, [0x0, 0x0, 0x1, 0x7])],
                [new EncodedReplayAccessUnit(startTimeNs, true, true, [0x0, 0x0, 0x1, 0x5])]);
            return true;
        }
    }

    private class FakeLiveDataPublisher : IRecorderLiveAnalysisPublisher
    {
        private readonly object _Gate = new();
        private readonly List<ReplayMessage> _ReplayMessages = [];
        private TaskCompletionSource<ReplayMessage>? _ReplayPublished;
        private int _ReplayPublishedCount;
        private readonly TaskCompletionSource _PublishStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _PublishAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool BlockPublishing { get; init; }

        public int PublishReplayCallCount { get; private set; }

        public int PublishReplayStartedCallCount { get; private set; }

        public ReplayStartedMessage? LastReplayStarted { get; private set; }

        public Task PublishReplayStarted(ReplayStartedMessage replayStarted, CancellationToken ct = default)
        {
            PublishReplayStartedCallCount++;
            LastReplayStarted = replayStarted;
            return Task.CompletedTask;
        }

        public async Task PublishReplay(ReplayMessage replay, CancellationToken ct = default)
        {
            PublishReplayCallCount++;
            _PublishStarted.TrySetResult();

            if (BlockPublishing)
            {
                await _PublishAllowed.Task.WaitAsync(ct);
            }

            TaskCompletionSource<ReplayMessage>? replayPublished = null;

            lock (_Gate)
            {
                _ReplayMessages.Add(replay);

                if (_ReplayPublished != null &&
                    _ReplayMessages.Count >= _ReplayPublishedCount)
                {
                    replayPublished = _ReplayPublished;
                    _ReplayPublished = null;
                }
            }

            replayPublished?.TrySetResult(replay);
        }

        public Task PublishVisionContext(VisionContextMessage visionContext, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishBallDetectionMask(BallDetectionMaskMessage ballDetectionMask, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<ReplayMessage> WaitForReplay(int count = 1)
        {
            lock (_Gate)
            {
                if (_ReplayMessages.Count >= count)
                {
                    return Task.FromResult(_ReplayMessages[count - 1]);
                }

                _ReplayPublishedCount = count;
                _ReplayPublished = new TaskCompletionSource<ReplayMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _ReplayPublished.Task.WaitAsync(TimeSpan.FromSeconds(1));
            }
        }

        public Task WaitUntilPublishStarted()
        {
            return _PublishStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }

        public void AllowPublish()
        {
            _PublishAllowed.TrySetResult();
        }
    }

    private class FakeLiveVideoStreamController : ILiveVideoStreamController
    {
        public List<string> Calls { get; } = [];

        public void PauseLiveVideoStream()
        {
            Calls.Add("pause");
        }

        public void ResumeLiveVideoStream()
        {
            Calls.Add("resume");
        }
    }
}
