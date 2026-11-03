// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using NSubstitute;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class TrainingPresenterTests
{
    [Fact]
    public void Training_uses_shared_table_tracking_mask_and_fps_presenters()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();
        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.TrainingRunning));
        context.EnableReplayAnalysisPrerequisites();

        context.PublishTrackingFrame(CreateTrackingFrame(12, 3_000_000_000, true));
        context.PublishProcessFrameRate(new ProcessFrameRateMessage { FramesPerSecond = 29.96 });
        context.PublishBallDetectionMask(new BallDetectionMaskMessage
        {
            FrameId = 12, TimestampNs = 3_000_000_000, Width = 2, Height = 2,
            Buffer = [1, 2, 3, 4], Length = 4,
        });

        Assert.Single(context.OverlaySink.TableStates);
        Assert.NotNull(Assert.Single(context.OverlaySink.TrackingStates).BallPosition);
        Assert.Single(context.OverlaySink.BallDetectionMaskStates);
        Assert.Equal(SessionMode.Training, context.UiSink.States[^1].Mode);
        Assert.True(context.UiSink.States[^1].IsRunning);
        Assert.Equal(30.0, context.UiSink.States[^1].ProcessFps);
    }

    [Fact]
    public void Training_pending_replay_preserves_fps_and_stop_rejects_old_replay()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();
        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.TrainingRunning));
        context.PublishProcessFrameRate(new ProcessFrameRateMessage { FramesPerSecond = 29.94 });
        context.PublishReplayStarted(CreateReplayStartedMessage());

        Assert.True(context.UiSink.States[^1].IsReplayActive);
        Assert.Equal(29.9, context.UiSink.States[^1].ProcessFps);

        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.Idle));
        context.PublishReplayStarted(CreateReplayStartedMessage());

        Assert.False(context.UiSink.States[^1].IsReplayActive);
        Assert.False(context.UiSink.States[^1].IsRunning);
        Assert.Null(context.UiSink.States[^1].ProcessFps);
        Assert.Contains("stop-playback", context.Events);
    }

    [Fact]
    public void New_training_session_resets_tracking_and_pending_replay()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();
        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.TrainingRunning));
        context.PublishReplayStarted(CreateReplayStartedMessage());
        int cleared = context.OverlaySink.ClearTrackingStateCalls;
        RecorderRuntimeStateChanged next = new()
        {
            Mode = RecorderRuntimeMode.TrainingRunning,
            ActiveSessionId = Guid.NewGuid(),
        };

        sut.OnRecorderRuntimeStateChanged(next);
        context.PublishReplayStarted(CreateReplayStartedMessage());

        Assert.True(context.OverlaySink.ClearTrackingStateCalls > cleared);
        Assert.False(context.UiSink.States[^1].IsReplayActive);
    }

    [Fact]
    public void Dispose_training_removes_subscriptions_and_resets_display()
    {
        SessionContext context = new();
        ActiveSession sut = context.CreateSut();
        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.TrainingRunning));
        context.EnableReplayAnalysisPrerequisites();
        context.PublishReplayStarted(CreateReplayStartedMessage());

        sut.Dispose();
        sut.Dispose();

        Assert.Equal(8, context.Subscriptions.Count);
        Assert.All(context.Subscriptions, subscription => subscription.Received(1).Dispose());
        Assert.Equal(TableOverlayState.Empty, context.OverlaySink.TableStates[^1]);
        Assert.True(context.OverlaySink.ClearBallDetectionMaskStateCalls > 0);
        Assert.False(context.UiSink.States[^1].IsConnected);
        Assert.False(context.UiSink.States[^1].IsReplayActive);
        Assert.Null(context.UiSink.States[^1].ProcessFps);
        Assert.Contains("stop-playback", context.Events);
    }
}
