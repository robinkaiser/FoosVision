// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Adapters.Common.Live;
using FoosVision.Adapters.Recorder.Replay;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Adapters.Recorder.Training.Control;
using FoosVision.Adapters.Recorder.Training;
using FoosVision.Ports.Media;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Events;
using FoosVision.UseCases.Training;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Training;

public class TrainingEventPresenterTests
{
    [Theory]
    [InlineData(RecorderStateChangeReason.CommandCompleted)]
    [InlineData(RecorderStateChangeReason.EndOfInput)]
    [InlineData(RecorderStateChangeReason.SourceStopped)]
    [InlineData(RecorderStateChangeReason.InternalError)]
    public async Task Training_stop_unsubscribes_stops_announcements_and_dumps_video(RecorderStateChangeReason reason)
    {
        Guid session = Guid.NewGuid();
        RecordingFeed feed = new();
        FrameProcessingLoop loop = new(feed, Substitute.For<IFrameProcessor>());
        RecorderRuntimeStateController runtime = new(Substitute.For<IRecorderEventPublisher>());
        ITrainingAnnouncements speech = Substitute.For<ITrainingAnnouncements>();
        IVideoDumpOrchestrator dumps = Substitute.For<IVideoDumpOrchestrator>();
        IReplayCoordinator replay = Substitute.For<IReplayCoordinator>();
        TrainingEventPresenter presenter = new(loop, replay, runtime, dumps, speech, reason);
        try
        {
            await presenter.ReportStarted(new(session));
            Assert.Equal(1, feed.Subscribers);
            replay.Received(1).StartSession(session);
            Assert.Equal(RecorderRuntimeMode.TrainingRunning, runtime.CurrentState.Mode);
            await presenter.ReportStopped(new(session));
            Assert.Equal(0, feed.Subscribers);
            replay.Received(1).StopSession(session);
            speech.Received(1).StopSession(session);
            speech.DidNotReceive().Invalidate(Arg.Any<TrainingAnnouncementInvalidation>());
            Assert.Equal(RecorderRuntimeMode.Idle, runtime.CurrentState.Mode);
            Assert.Equal(reason, runtime.CurrentState.Reason);
            dumps.Received(1).TryScheduleDump(VideoDumpSessionKind.Training);
        }
        finally { loop.Stop(); }
    }

    [Fact]
    public async Task Rejected_commands_preserve_the_current_session()
    {
        Guid current = Guid.NewGuid();
        RecordingFeed feed = new();
        FrameProcessingLoop loop = new(feed, Substitute.For<IFrameProcessor>());
        RecorderRuntimeStateController runtime = new(Substitute.For<IRecorderEventPublisher>());
        await runtime.PublishIfChanged(RecorderRuntimeMode.GameRunning, current,
            RecorderStateChangeReason.CommandCompleted, "", TestContext.Current.CancellationToken);
        ITrainingAnnouncements speech = Substitute.For<ITrainingAnnouncements>();
        IReplayCoordinator replay = Substitute.For<IReplayCoordinator>();
        TrainingEventPresenter presenter = new(loop, replay, runtime, Substitute.For<IVideoDumpOrchestrator>(), speech);
        await presenter.ReportStartFailed("busy");
        await presenter.ReportStopFailed("not active");
        Assert.Equal(current, runtime.CurrentState.ActiveSessionId);
        Assert.Equal(RecorderRuntimeMode.GameRunning, runtime.CurrentState.Mode);
        Assert.Equal(1, runtime.CurrentState.Sequence);
        Assert.Equal(0, feed.Subscribers);
        Assert.Empty(speech.ReceivedCalls());
        Assert.Empty(replay.ReceivedCalls());
    }

    private sealed class RecordingFeed : IFrameFeed
    {
        public int Subscribers { get; private set; }
        public event Action<IFrameHandle> FrameReady
        {
            add => Subscribers++;
            remove => Subscribers--;
        }
        public bool TryAcquireById(ulong id, [NotNullWhen(true)] out IFrameHandle? handle)
        {
            handle = null;
            return false;
        }
    }
}
