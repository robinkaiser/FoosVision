// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Replay;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.ValueObjects;
using System.Threading.Channels;
using FoosVision.Domain.Game.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Replay;

public class ReplayCoordinatorTests
{
    private const long _1s = 1000L * 1_000_000L;
    private const long _700ms = 700L * 1_000_000L;

    [Fact]
    public async Task RequestReturnToLive_publishes_correlated_recorder_decision()
    {
        IRecorderLiveAnalysisPublisher publisher = Substitute.For<IRecorderLiveAnalysisPublisher>();
        using ReplayCoordinator testee = new(new FakeReplayBuffer(), new FakeLiveVideoStreamController(), publisher);
        Guid sessionId = Guid.NewGuid();
        testee.StartSession(sessionId);
        Frame trigger = new(42, 123456);
        await testee.RequestReplay(sessionId, trigger,
            GameReplayRules.Create(new(trigger, new(100, 200), BallPossession.None, 0, ReplayTriggerKind.BallDisappeared)));
        await testee.RequestReturnToLive(sessionId, trigger);
        await publisher.Received(1).PublishReplayReturnToLive(Arg.Is<ReplayReturnToLiveMessage>(message =>
            message.SessionId == sessionId && message.TriggerFrameId == trigger.Id && message.TriggerTimestampNs == trigger.TimestampNs),
            Arg.Any<CancellationToken>());
        await testee.RequestReturnToLive(Guid.NewGuid(), trigger);
        await testee.RequestReturnToLive(sessionId, new(41, 123455));
        await publisher.Received(1).PublishReplayReturnToLive(Arg.Any<ReplayReturnToLiveMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestReplay_publishes_replay_from_anchor_frame_for_one_second()
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new();
        FakeLiveVideoStreamController liveVideoStream = new();
        using ReplayCoordinator testee = new(replayBuffer, liveVideoStream, publisher);

        Guid sessionId = Guid.NewGuid();
        testee.StartSession(sessionId);
        Frame triggerFrame = new(12, 5 * _1s);
        Frame anchorFrame = new(10, 3 * _1s);
        Point anchorPosition = new(100, 200);
        BallPossession anchorPossession = new(Team.A, PossessionArea.ThreeBar);
        const int anchorPossessionTimeMs = 9250;

        await testee.RequestReplay(sessionId, triggerFrame,
            GameReplayRules.Create(new(anchorFrame, anchorPosition, anchorPossession, anchorPossessionTimeMs, ReplayTriggerKind.BallDisappeared)));
        ReplayClipMessage message = await publisher.WaitForReplay();

        Assert.Equal(sessionId, message.SessionId);
        Assert.Equal(sessionId, publisher.LastReplayStarted?.SessionId);
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
        testee.StartSession(Guid.Empty);

        Task request = testee.RequestReplay(Guid.Empty, new Frame(12, 5 * _1s),
            GameReplayRules.Create(new(new Frame(10, 3 * _1s), new Point(100, 200), new BallPossession(Team.A, PossessionArea.ThreeBar), 9250, ReplayTriggerKind.BallDisappeared)));

        Assert.True(request.IsCompletedSuccessfully);
        await publisher.WaitUntilPublishStarted();

        publisher.AllowPublish();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequestReplay_keeps_newest_pending_request_while_first_is_being_sent(bool supersedePending)
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new() { BlockPublishing = true };
        FakeLiveVideoStreamController liveVideoStream = new();
        using ReplayCoordinator testee = new(replayBuffer, liveVideoStream, publisher);
        testee.StartSession(Guid.Empty);

        await testee.RequestReplay(Guid.Empty, new Frame(12, 5 * _1s),
            GameReplayRules.Create(new(new Frame(10, 3 * _1s), new Point(100, 200), new BallPossession(Team.A, PossessionArea.ThreeBar), 9250, ReplayTriggerKind.BallDisappeared)));
        await publisher.WaitUntilPublishStarted();

        await testee.RequestReplay(Guid.Empty, new Frame(22, 8 * _1s),
            GameReplayRules.Create(new(new Frame(20, 6 * _1s), new Point(300, 400), new BallPossession(Team.B, PossessionArea.FiveBar), 10_000, ReplayTriggerKind.BallDisappeared)));

        if (supersedePending)
        {
            await testee.RequestReplay(Guid.Empty, new(32, 11 * _1s),
                GameReplayRules.Create(new(new(30, 9 * _1s), new(500, 400), BallPossession.None, 0, ReplayTriggerKind.BallDisappeared)));
        }
        publisher.AllowPublish();
        ReplayClipMessage newest = await publisher.WaitForReplay(2);

        Assert.Equal(supersedePending ? 32UL : 22UL, newest.TriggerFrameId);
        Assert.Equal(2, publisher.PublishReplayClipCallCount);
    }

    [Fact]
    public async Task RequestReplay_accepts_second_request_after_first_was_sent()
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new();
        FakeLiveVideoStreamController liveVideoStream = new();
        ManualTaskScheduler scheduler = new();
        using ReplayCoordinator testee = new(replayBuffer, liveVideoStream, publisher, scheduler);
        testee.StartSession(Guid.Empty);

        await testee.RequestReplay(Guid.Empty, new Frame(12, 5 * _1s),
            GameReplayRules.Create(new(new Frame(10, 3 * _1s), new Point(100, 200), new BallPossession(Team.A, PossessionArea.ThreeBar), 9250, ReplayTriggerKind.BallDisappeared)));
        // Drain the first queued request before submitting another one.
        await scheduler.RunNext();
        ReplayClipMessage firstMessage = await publisher.WaitForReplay();

        await testee.RequestReplay(Guid.Empty, new Frame(22, 8 * _1s),
            GameReplayRules.Create(new(new Frame(20, 6 * _1s), new Point(300, 400), new BallPossession(Team.B, PossessionArea.FiveBar), 10_000, ReplayTriggerKind.BallDisappeared)));
        await scheduler.RunNext();
        ReplayClipMessage secondMessage = await publisher.WaitForReplay(2);

        Assert.Equal(12UL, firstMessage.TriggerFrameId);
        Assert.Equal(22UL, secondMessage.TriggerFrameId);
        Assert.Equal(2, publisher.PublishReplayClipCallCount);
        Assert.Equal(2, publisher.PublishReplayStartedCallCount);
        Assert.Equal(["pause", "resume", "pause", "resume"], liveVideoStream.Calls);
        // Allow shutdown continuations to run automatically during Dispose.
        scheduler.RunInline = true;
    }

    [Fact]
    public async Task RequestReplay_uses_seven_hundred_milliseconds_for_saved_shot()
    {
        FakeReplayBuffer replayBuffer = new();
        FakeLiveDataPublisher publisher = new();
        using ReplayCoordinator testee = new(replayBuffer, new FakeLiveVideoStreamController(), publisher);
        testee.StartSession(Guid.Empty);

        Frame triggerFrame = new(12, 5 * _1s);
        Frame anchorFrame = new(10, 3 * _1s);

        await testee.RequestReplay(Guid.Empty, triggerFrame,
            GameReplayRules.Create(new(anchorFrame, new Point(100, 200), new BallPossession(Team.A, PossessionArea.ThreeBar), 9250, ReplayTriggerKind.SavedShot)));
        ReplayClipMessage message = await publisher.WaitForReplay();

        Assert.Equal(anchorFrame.TimestampNs, message.ReplayStartTimestampNs);
        Assert.Equal(anchorFrame.TimestampNs + _700ms, message.ReplayEndTimestampNs);
        Assert.Equal(anchorFrame.TimestampNs, replayBuffer.RequestedStartTimeNs);
        Assert.Equal(anchorFrame.TimestampNs + _700ms, replayBuffer.RequestedEndTimeNs);
    }

    [Fact]
    public async Task Training_window_waits_for_tail_without_blocking_ready_and_publishes_playback_settings()
    {
        FakeReplayBuffer buffer = new() { IncompleteStartTimeNs = 850_000_000 };
        FakeLiveDataPublisher publisher = new();
        ManualTimeProvider time = new();
        using ReplayCoordinator testee = new(buffer, new FakeLiveVideoStreamController(), publisher, timeProvider: time);
        testee.StartSession(Guid.Empty);
        Frame trigger = new(12, 1_100_000_000);
        ReplayPlan replay = TrainingPlan() with { Speed = 0.5, RequiredCompletedLoops = 2 };
        await testee.RequestReplay(Guid.Empty, trigger, replay);
        ManualTimeProvider.ManualTimer timer = await time.NextTimer();
        Assert.Equal(0, publisher.PublishReplayStartedCallCount);
        await testee.RequestReturnToLive(Guid.Empty, trigger);
        Assert.Equal(trigger.Id, publisher.LastReturnToLive?.TriggerFrameId);
        buffer.IncompleteStartTimeNs = null;
        timer.Fire();
        ReplayClipMessage clip = await publisher.WaitForReplay();
        Assert.Equal(850_000_000, buffer.RequestedStartTimeNs);
        Assert.Equal(1_250_000_000, buffer.RequestedEndTimeNs);
        Assert.Equal(0.5, clip.Speed);
        Assert.Equal(2, clip.RequiredCompletedLoops);
        Assert.Equal(clip.Speed, publisher.LastReplayStarted?.Speed);
        Assert.Equal(clip.RequiredCompletedLoops, publisher.LastReplayStarted?.RequiredCompletedLoops);
        Assert.Equal(1, publisher.PublishReplayClipCallCount);
    }

    [Fact]
    public async Task Missing_tail_is_dropped_after_bounded_wait_and_next_replay_can_run()
    {
        FakeReplayBuffer buffer = new() { IncompleteStartTimeNs = 850_000_000 };
        FakeLiveDataPublisher publisher = new();
        ManualTimeProvider time = new();
        using ReplayCoordinator testee = new(buffer, new FakeLiveVideoStreamController(), publisher, timeProvider: time);
        testee.StartSession(Guid.Empty);
        await testee.RequestReplay(Guid.Empty, new(12, _1s), TrainingPlan());
        ManualTimeProvider.ManualTimer timer = await time.NextTimer();
        // Queue a complete second clip; it can only publish after the first request is abandoned.
        await testee.RequestReplay(Guid.Empty, new(22, 3 * _1s), TrainingPlan() with { StartTimestampNs = 2 * _1s, EndTimestampNs = 3 * _1s });
        timer.Fire();
        for (int i = 1; i < 50; i++)
        {
            (await time.NextTimer()).Fire();
        }
        ReplayClipMessage clip = await publisher.WaitForReplay();
        Assert.Equal(22UL, clip.TriggerFrameId);
        Assert.Equal(1, publisher.PublishReplayStartedCallCount);
        Assert.Equal(TimeSpan.FromSeconds(1), time.TotalDelay);
    }

    [Fact]
    public async Task Unavailable_segment_does_not_pause_live_or_announce_a_replay()
    {
        FakeReplayBuffer buffer = new() { Available = false };
        FakeLiveDataPublisher publisher = new();
        FakeLiveVideoStreamController live = new();
        ManualTaskScheduler scheduler = new();
        using ReplayCoordinator testee = new(buffer, live, publisher, scheduler);
        testee.StartSession(Guid.Empty);
        await testee.RequestReplay(Guid.Empty, new(12, _1s), TrainingPlan());
        await scheduler.RunNext();
        Assert.Equal(0, publisher.PublishReplayStartedCallCount);
        Assert.Equal(0, publisher.PublishReplayClipCallCount);
        Assert.Empty(live.Calls);
        scheduler.RunInline = true;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Session_end_cancels_tail_wait_and_discards_queued_replay(bool replaceSession)
    {
        FakeReplayBuffer buffer = new() { IncompleteStartTimeNs = 850_000_000 };
        FakeLiveDataPublisher publisher = new();
        ManualTimeProvider time = new();
        using ReplayCoordinator testee = new(buffer, new FakeLiveVideoStreamController(), publisher, timeProvider: time);
        testee.StartSession(Guid.Empty);
        Frame trigger = new(12, _1s);
        await testee.RequestReplay(Guid.Empty, trigger, TrainingPlan());
        await time.NextTimer();
        await testee.RequestReplay(Guid.Empty, new(22, 2 * _1s), TrainingPlan());
        Guid nextSession = Guid.NewGuid();
        if (!replaceSession)
        {
            testee.StopSession(Guid.Empty);
        }
        testee.StartSession(nextSession);
        await testee.RequestReturnToLive(Guid.Empty, trigger);
        await testee.RequestReplay(Guid.Empty, trigger, TrainingPlan());
        await testee.RequestReplay(nextSession, new(32, 3 * _1s), TrainingPlan() with { StartTimestampNs = 2 * _1s, EndTimestampNs = 3 * _1s });
        ReplayClipMessage clip = await publisher.WaitForReplay();
        Assert.Equal(nextSession, clip.SessionId);
        Assert.Equal(32UL, clip.TriggerFrameId);
        Assert.Equal(1, publisher.PublishReplayClipCallCount);
        Assert.Null(publisher.LastReturnToLive);
    }

    [Fact]
    public async Task Stop_cancels_inflight_transfer_and_resumes_live_stream()
    {
        FakeLiveDataPublisher publisher = new() { BlockPublishing = true };
        FakeLiveVideoStreamController live = new();
        using ReplayCoordinator testee = new(new FakeReplayBuffer(), live, publisher);
        testee.StartSession(Guid.Empty);
        await testee.RequestReplay(Guid.Empty, new(12, _1s), TrainingPlan());
        await publisher.WaitUntilPublishStarted();
        testee.StopSession(Guid.Empty);
        await live.Resumed.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.Equal(["pause", "resume"], live.Calls);
        Assert.Equal(0, publisher.PublishedCount);
    }

    private static ReplayPlan TrainingPlan()
        => new(new(10, _1s), new(100, 200), new(Team.A, PossessionArea.FiveBar), 0,
            850_000_000, 1_250_000_000, 0.25, 1);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly Channel<ManualTimer> _Timers = Channel.CreateUnbounded<ManualTimer>();
        public TimeSpan TotalDelay { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            TotalDelay += dueTime;
            ManualTimer timer = new(callback, state);
            _Timers.Writer.TryWrite(timer);
            return timer;
        }

        public ValueTask<ManualTimer> NextTimer() => _Timers.Reader.ReadAsync(TestContext.Current.CancellationToken);

        public sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
        {
            public void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class ManualTaskScheduler : TaskScheduler
    {
        private readonly Lock _Gate = new();
        private readonly Queue<Task> _Tasks = new();
        private readonly SemaphoreSlim _Queued = new(0);

        public bool RunInline { get; set; }

        public async Task RunNext()
        {
            await _Queued.WaitAsync(TestContext.Current.CancellationToken);
            Task task;
            lock (_Gate)
            {
                task = _Tasks.Dequeue();
            }
            Execute(task);
        }

        private void Execute(Task task)
        {
            SynchronizationContext? previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                TryExecuteTask(task);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        protected override void QueueTask(Task task)
        {
            if (RunInline)
            {
                Execute(task);
                return;
            }
            lock (_Gate)
            {
                _Tasks.Enqueue(task);
            }
            _Queued.Release();
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;

        protected override IEnumerable<Task> GetScheduledTasks()
        {
            lock (_Gate) return _Tasks.ToArray();
        }
    }

    private class FakeReplayBuffer : IEncodedReplayBuffer
    {
        public bool Available { get; init; } = true;
        public long? IncompleteStartTimeNs { get; set; }
        public long RequestedStartTimeNs { get; private set; }

        public long RequestedEndTimeNs { get; private set; }

        public bool TryGetReplaySegment(long startTimeNs, long endTimeNs, out EncodedReplaySegment segment)
        {
            RequestedStartTimeNs = startTimeNs;
            RequestedEndTimeNs = endTimeNs;
            segment = new EncodedReplaySegment(
                EncodedReplayCodec.H264,
                startTimeNs,
                IncompleteStartTimeNs == startTimeNs ? endTimeNs - 150_000_000 : endTimeNs,
                [new EncodedReplayParameterSet(EncodedReplayParameterSetType.SPS, [0x0, 0x0, 0x1, 0x7])],
                [new EncodedReplayAccessUnit(startTimeNs, true, true, [0x0, 0x0, 0x1, 0x5])]);
            return Available;
        }
    }

    private class FakeLiveDataPublisher : IRecorderLiveAnalysisPublisher
    {
        private readonly Lock _Gate = new();
        private readonly List<ReplayClipMessage> _ReplayMessages = [];
        private TaskCompletionSource<ReplayClipMessage>? _ReplayPublished;
        private int _ReplayPublishedCount;
        private readonly TaskCompletionSource _PublishStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _PublishAllowed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PublishedCount { get { lock (_Gate) return _ReplayMessages.Count; } }

        public bool BlockPublishing { get; init; }

        public int PublishReplayClipCallCount { get; private set; }

        public int PublishReplayStartedCallCount { get; private set; }

        public ReplayStartedMessage? LastReplayStarted { get; private set; }
        public ReplayReturnToLiveMessage? LastReturnToLive { get; private set; }

        public Task PublishReplayStarted(ReplayStartedMessage replayStarted, CancellationToken ct = default)
        {
            PublishReplayStartedCallCount++;
            LastReplayStarted = replayStarted;
            return Task.CompletedTask;
        }

        public async Task PublishReplayClip(ReplayClipMessage replay, CancellationToken ct = default)
        {
            PublishReplayClipCallCount++;
            _PublishStarted.TrySetResult();

            if (BlockPublishing)
            {
                await _PublishAllowed.Task.WaitAsync(ct);
            }

            TaskCompletionSource<ReplayClipMessage>? replayPublished = null;

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

        public Task PublishReplayReturnToLive(ReplayReturnToLiveMessage message, CancellationToken ct = default)
        {
            LastReturnToLive = message;
            return Task.CompletedTask;
        }

        public Task PublishBallDetectionMask(BallDetectionMaskMessage ballDetectionMask, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<ReplayClipMessage> WaitForReplay(int count = 1)
        {
            lock (_Gate)
            {
                if (_ReplayMessages.Count >= count)
                {
                    return Task.FromResult(_ReplayMessages[count - 1]);
                }

                _ReplayPublishedCount = count;
                _ReplayPublished = new TaskCompletionSource<ReplayClipMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
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
        public TaskCompletionSource Resumed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void PauseLiveVideoStream()
        {
            Calls.Add("pause");
        }

        public void ResumeLiveVideoStream()
        {
            Calls.Add("resume");
            Resumed.TrySetResult();
        }
    }
}
