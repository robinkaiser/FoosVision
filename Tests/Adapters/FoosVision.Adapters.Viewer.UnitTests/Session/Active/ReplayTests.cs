// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Protocol.Messages.LiveAnalysis;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class ReplayTests
{
    [Fact]
    public async Task HandleReplay_stops_live_playback_and_shows_replay_analysis()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        context.EnableReplayAnalysisPrerequisites();
        sut.HandleReplay(CreateReplayMessage());

        await ReplayCoordinatorContext.WaitUntil(() => context.OverlaySink.TrackingStates.Count == 1);

        Assert.Contains("stop-playback", context.Events);
        Assert.Contains("start-replay-playback", context.Events);
        Assert.True(context.OverlaySink.ClearTrackingStateCalls >= 1);
        TrackingOverlayState replayState = context.OverlaySink.TrackingStates[^1];
        Assert.True(replayState.Trail.Count > 1);
        Assert.Equal(Team.B, replayState.PossessingTeam);
        Assert.Equal(PossessionArea.FiveBar, replayState.PossessionArea);
        Assert.Equal(9266, replayState.PossessionTimeMs);

        await sut.ObserveLiveTrackingAsync(new Point(960, 540));

        Assert.Single(context.OverlaySink.TrackingStates);
    }

    [Fact]
    public async Task HandleReplayStarted_stops_live_playback_without_showing_replay_analysis()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        sut.HandleReplayStarted(CreateReplayStartedMessage());

        await ReplayCoordinatorContext.WaitUntil(() => context.Events.Contains("stop-playback"));

        Assert.True(context.OverlaySink.ClearTrackingStateCalls >= 1);
        Assert.DoesNotContain("start-replay-playback", context.Events);
        Assert.Empty(context.OverlaySink.TrackingStates);

        await sut.ObserveLiveTrackingAsync(new Point(960, 540));

        Assert.Empty(context.OverlaySink.TrackingStates);
    }

    [Fact]
    public async Task Replay_started_publishes_replay_fps_state()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.GameRunning));
        context.PublishReplayStarted(CreateReplayStartedMessage());

        await context.WaitUntil(() => context.UiSink.States[^1].IsReplayActive);

        Assert.True(context.UiSink.States[^1].IsRunning);
        Assert.Equal(120.0, context.UiSink.States[^1].TrackingFps);
    }

    [Fact]
    public async Task Replay_message_after_started_shows_replay_analysis_and_starts_replay_playback()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        context.EnableReplayAnalysisPrerequisites();
        sut.HandleReplayStarted(CreateReplayStartedMessage());
        await ReplayCoordinatorContext.WaitUntil(() => context.Events.Contains("stop-playback"));

        sut.HandleReplay(CreateReplayMessage());

        await ReplayCoordinatorContext.WaitUntil(() => context.OverlaySink.TrackingStates.Count == 1);

        Assert.Contains("start-replay-playback", context.Events);
        Assert.Single(context.OverlaySink.TrackingStates);
        Assert.True(context.OverlaySink.TrackingStates[^1].Trail.Count > 1);
    }

    [Fact]
    public async Task Replay_started_stops_current_replay_playback_immediately()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        context.EnableReplayAnalysisPrerequisites();
        sut.HandleReplay(CreateReplayMessage());
        await ReplayCoordinatorContext.WaitUntil(() => context.PlaybackController.ReplayStartTimestampNs.Count == 1);

        sut.HandleReplayStarted(CreateReplayStartedMessage(
            triggerFrameId: 84,
            triggerTimestampNs: 4_000_000_000,
            anchorFrameId: 80,
            anchorTimestampNs: 3_900_000_000,
            replayEndTimestampNs: 4_900_000_000));

        await ReplayCoordinatorContext.WaitUntil(() => context.Events.Count(e => e == "stop-playback") == 2);

        Assert.Single(context.PlaybackController.ReplayStartTimestampNs);

        Assert.True(sut.IsReplayActive);
        sut.HandleReplay(CreateReplayMessage(
            triggerFrameId: 84,
            triggerTimestampNs: 4_000_000_000,
            anchorFrameId: 80,
            anchorTimestampNs: 3_900_000_000,
            replayEndTimestampNs: 4_900_000_000));

        await ReplayCoordinatorContext.WaitUntil(() => context.PlaybackController.ReplayStartTimestampNs.Count == 2);

        Assert.Equal([1_900_000_000, 3_900_000_000], context.PlaybackController.ReplayStartTimestampNs);
    }

    [Fact]
    public async Task Replay_message_cancels_previous_replay_analysis_before_starting_replacement()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        context.EnableReplayAnalysisPrerequisites();
        context.ReplayFrameDecoder.BlockFirstDecode = true;

        sut.HandleReplay(CreateReplayMessage());
        await context.ReplayFrameDecoder.WaitUntilFirstDecodeBlocked();

        sut.HandleReplay(CreateReplayMessage(
            triggerFrameId: 84,
            triggerTimestampNs: 4_000_000_000,
            anchorFrameId: 80,
            anchorTimestampNs: 3_900_000_000,
            replayEndTimestampNs: 4_900_000_000));

        await ReplayCoordinatorContext.WaitUntil(() => context.PlaybackController.ReplayStartTimestampNs.Count == 1);

        Assert.True(context.ReplayFrameDecoder.WasFirstDecodeCanceled);
        Assert.Equal(2, context.ReplayFrameDecoder.DecodeCallCount);
        Assert.Equal(3_900_000_000, context.PlaybackController.ReplayStartTimestampNs.Single());
    }

    [Fact]
    public async Task Tracking_frame_returns_to_live_after_one_replay_loop_when_live_ball_moves_ten_pixels()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        context.EnableReplayAnalysisPrerequisites();
        sut.HandleReplay(CreateReplayMessage());
        await ReplayCoordinatorContext.WaitUntil(() => context.OverlaySink.TrackingStates.Count == 1);

        await context.PlaybackController.CompleteReplayLoopAsync();
        await sut.ObserveLiveTrackingAsync(new Point(100, 200));
        await sut.ObserveLiveTrackingAsync(new Point(109, 200));

        Assert.DoesNotContain("start-playback", context.Events);

        await sut.ObserveLiveTrackingAsync(new Point(110, 200));

        await ReplayCoordinatorContext.WaitUntil(() => context.Events.Count(e => e == "start-playback") == 1);

        Assert.Equal(["clear-tracking", "stop-playback", "start-replay-playback", "clear-tracking", "stop-playback", "start-playback"], context.Events);
        Assert.False(sut.IsReplayActive);
    }

    [Fact]
    public async Task Replay_with_h265_keeps_live_playback_and_does_not_start_replay()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        context.EnableReplayAnalysisPrerequisites();
        ReplayMessage message = CreateReplayMessage() with
        {
            Codec = EncodedReplayCodecMessage.H265,
        };
        sut.HandleReplay(message);

        await Task.Yield();

        Assert.DoesNotContain("stop-playback", context.Events);
        Assert.DoesNotContain("start-replay-playback", context.Events);
        Assert.Empty(context.OverlaySink.TrackingStates);
    }

    [Fact]
    public async Task Replay_without_table_configuration_is_ignored()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        context.ApplyVisionContext();
        sut.HandleReplay(CreateReplayMessage());

        await Task.Yield();

        Assert.DoesNotContain("stop-playback", context.Events);
        Assert.DoesNotContain("start-replay-playback", context.Events);
        Assert.Equal(0, context.ReplayFrameDecoder.DecodeCallCount);
    }

    [Fact]
    public async Task Replay_without_vision_context_is_ignored()
    {
        using ReplayCoordinatorContext context = new();
        ViewerReplayCoordinator sut = context.Sut;

        context.ApplyTableConfiguration();
        sut.HandleReplay(CreateReplayMessage());

        await Task.Yield();

        Assert.DoesNotContain("stop-playback", context.Events);
        Assert.DoesNotContain("start-replay-playback", context.Events);
        Assert.Equal(0, context.ReplayFrameDecoder.DecodeCallCount);
    }
}
