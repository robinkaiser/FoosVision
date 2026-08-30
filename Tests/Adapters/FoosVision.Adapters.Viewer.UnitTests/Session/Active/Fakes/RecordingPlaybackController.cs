// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Playback;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class RecordingPlaybackController : IPlaybackController
{
    private readonly List<string> _Events;

    public RecordingPlaybackController(List<string> events)
    {
        _Events = events;
    }

    public event Func<Task>? ReplayLoopCompleted;

    public event Func<long, Task>? ReplayPositionChanged;

    public List<long> ReplayStartTimestampNs { get; } = [];

    public async Task CompleteReplayLoopAsync()
    {
        Func<Task>? replayLoopCompleted = ReplayLoopCompleted;
        if (replayLoopCompleted != null)
        {
            await replayLoopCompleted();
        }
    }

    public Task StartAsync(PlaybackRequest playbackRequest)
    {
        if (playbackRequest.Kind == PlaybackKind.LiveStream)
        {
            _Events.Add("start-playback");
            Assert.Equal("cache://stream.sdp", playbackRequest.MediaSource);
            return Task.CompletedTask;
        }

        _Events.Add("start-replay-playback");
        Assert.Equal(PlaybackKind.EncodedReplay, playbackRequest.Kind);
        Assert.NotNull(playbackRequest.EncodedReplay);
        Assert.Equal(PlaybackCodec.H264, playbackRequest.EncodedReplay.Codec);
        Assert.Equal(0.25D, playbackRequest.EncodedReplay.Speed);
        Assert.Equal(2, playbackRequest.EncodedReplay.AccessUnits.Count);
        ReplayStartTimestampNs.Add(playbackRequest.EncodedReplay.ReplayStartTimestampNs);

        Func<long, Task>? replayPositionChanged = ReplayPositionChanged;
        if (replayPositionChanged != null)
        {
            return replayPositionChanged(playbackRequest.EncodedReplay.AccessUnits[^1].TimeNs);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _Events.Add("stop-playback");
        return Task.CompletedTask;
    }
}
