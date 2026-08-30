// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Playback;
using AndroidView = Android.Views.View;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Stage;

public class PlaybackController : IPlaybackController
{
    private readonly AndroidView _View;
    private readonly VideoPlayer _VideoPlayer;

    public PlaybackController(AndroidView view, VideoPlayer videoPlayer)
    {
        _View = view;
        _VideoPlayer = videoPlayer;
        _VideoPlayer.ReplayLoopCompleted += OnReplayLoopCompleted;
        _VideoPlayer.ReplayPositionChanged += OnReplayPositionChanged;
    }

    public event Func<Task>? ReplayLoopCompleted;

    public event Func<long, Task>? ReplayPositionChanged;

    public Task StartAsync(PlaybackRequest playbackRequest)
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _View.Post(async () =>
        {
            try
            {
                await _VideoPlayer.StartAsync(playbackRequest);
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }

    public Task StopAsync()
    {
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _View.Post(async () =>
        {
            try
            {
                await _VideoPlayer.StopAsync();
                completion.TrySetResult();
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        });

        return completion.Task;
    }

    private Task OnReplayLoopCompleted()
    {
        Func<Task>? replayLoopCompleted = ReplayLoopCompleted;
        return replayLoopCompleted?.Invoke() ?? Task.CompletedTask;
    }

    private Task OnReplayPositionChanged(long timeNs)
    {
        Func<long, Task>? replayPositionChanged = ReplayPositionChanged;
        return replayPositionChanged?.Invoke(timeNs) ?? Task.CompletedTask;
    }
}
