// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Protocol.Messages.LiveAnalysis;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class ReturnToLiveTests
{
    [Fact]
    public async Task Old_loop_callback_during_replacement_does_not_complete_new_replay()
    {
        using ReplayCoordinatorContext context = new();
        context.EnableReplayAnalysisPrerequisites();
        context.Sut.HandleReplayClip(CreateReplayClipMessage());
        await context.PlaybackController.ReplayStarted.WaitAsync(TestContext.Current.CancellationToken);

        EncodedReplayPlayback firstPlayback = Assert.Single(context.PlaybackController.ReplayPlaybacks);
        TaskCompletionSource secondStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.PlaybackController.OnReplayStarting = playback =>
        {
            // The old decoder's completion can arrive while the new player is being started.
            _ = context.PlaybackController.CompleteReplayLoopAsync(firstPlayback);
            secondStarted.TrySetResult();
        };
        context.Sut.HandleReplayStarted(CreateReplayStartedMessage(84, 4_000_000_000, 80, 3_900_000_000, 4_900_000_000));
        context.Sut.HandleReplayReturnToLive(new() { SessionId = SessionId, TriggerFrameId = 84, TriggerTimestampNs = 4_000_000_000 });
        context.Sut.HandleReplayClip(CreateReplayClipMessage(84, 4_000_000_000, 80, 3_900_000_000, 4_900_000_000));
        await secondStarted.Task.WaitAsync(TestContext.Current.CancellationToken);

        Assert.True(context.ReplaySessionStore.HasActive);
        Assert.Equal(0, context.ReplaySessionStore.LoadActive().Value.CompletedLoops);
        Assert.DoesNotContain("start-playback", context.Events);
        await context.PlaybackController.CompleteReplayLoopAsync(context.PlaybackController.ReplayPlaybacks[^1]);
        await context.PlaybackController.LiveStarted.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Loop_callback_returns_without_waiting_for_the_player_to_stop()
    {
        using ReplayCoordinatorContext context = new();
        context.EnableReplayAnalysisPrerequisites();
        context.Sut.HandleReplayClip(CreateReplayClipMessage());
        await context.PlaybackController.ReplayStarted.WaitAsync(TestContext.Current.CancellationToken);
        context.Sut.HandleReplayReturnToLive(new() { SessionId = SessionId, TriggerFrameId = 42, TriggerTimestampNs = 2_000_000_000 });
        TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        context.PlaybackController.StopCompletion = stopped.Task;

        Task callback = context.PlaybackController.CompleteReplayLoopAsync();
        try
        {
            Assert.True(callback.IsCompletedSuccessfully);
        }
        finally
        {
            stopped.TrySetResult();
        }

        await context.PlaybackController.LiveStarted.WaitAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public void Disposed_receiver_ignores_late_clip_and_return_messages()
    {
        using ReplayCoordinatorContext context = new();
        context.Sut.HandleReplayStarted(CreateReplayStartedMessage());
        context.Sut.Dispose();
        int events = context.Events.Count;
        context.Sut.HandleReplayClip(CreateReplayClipMessage());
        context.Sut.HandleReplayReturnToLive(new() { SessionId = SessionId, TriggerFrameId = 42, TriggerTimestampNs = 2_000_000_000 });
        Assert.Equal(events, context.Events.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Return_before_clip_waits_for_first_loop_and_retired_clip_cannot_replace_live(bool receiveStarted)
    {
        using ReplayCoordinatorContext context = new();
        context.EnableReplayAnalysisPrerequisites();
        ReplayReturnToLiveMessage command = new() { SessionId = SessionId, TriggerFrameId = 42, TriggerTimestampNs = 2_000_000_000 };
        context.Sut.HandleReplayReturnToLive(command);
        if (receiveStarted)
        {
            context.Sut.HandleReplayStarted(CreateReplayStartedMessage());
        }

        context.Sut.HandleReplayClip(CreateReplayClipMessage());
        await context.PlaybackController.ReplayStarted.WaitAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("start-playback", context.Events);

        await context.PlaybackController.CompleteReplayLoopAsync();
        await context.PlaybackController.LiveStarted.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(context.Sut.IsReplayActive);
        int events = context.Events.Count;
        context.Sut.HandleReplayReturnToLive(command);
        context.Sut.HandleReplayStarted(CreateReplayStartedMessage());
        context.Sut.HandleReplayClip(CreateReplayClipMessage());
        Assert.Equal(events, context.Events.Count);
        Assert.Single(context.PlaybackController.ReplayStartTimestampNs);
    }

    [Fact]
    public async Task Loop_without_command_keeps_replay_and_later_command_returns_immediately()
    {
        using ReplayCoordinatorContext context = new();
        context.EnableReplayAnalysisPrerequisites();
        context.Sut.HandleReplayClip(CreateReplayClipMessage());
        await context.PlaybackController.ReplayStarted.WaitAsync(TestContext.Current.CancellationToken);
        await context.PlaybackController.CompleteReplayLoopAsync();
        Assert.True(context.Sut.IsReplayActive);
        Assert.DoesNotContain("start-playback", context.Events);

        context.Sut.HandleReplayReturnToLive(new() { SessionId = SessionId, TriggerFrameId = 42, TriggerTimestampNs = 2_000_000_000 });
        await context.PlaybackController.LiveStarted.WaitAsync(TestContext.Current.CancellationToken);
        Assert.False(context.Sut.IsReplayActive);
    }
}
