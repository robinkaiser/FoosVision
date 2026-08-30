// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session.Playback;

namespace FoosVision.Adapters.Viewer.Session.Active;

internal class ViewerPlaybackCoordinator : IDisposable
{
    private readonly IPlaybackSourceFactory _PlaybackSourceFactory;
    private readonly IPlaybackController _PlaybackController;
    private readonly SemaphoreSlim _Operations = new(1, 1);
    private int _Disposed;

    public ViewerPlaybackCoordinator(
        IPlaybackSourceFactory playbackSourceFactory,
        IPlaybackController playbackController)
    {
        _PlaybackSourceFactory = playbackSourceFactory;
        _PlaybackController = playbackController;
    }

    public event Func<Task>? ReplayLoopCompleted
    {
        add => _PlaybackController.ReplayLoopCompleted += value;
        remove => _PlaybackController.ReplayLoopCompleted -= value;
    }

    public event Func<long, Task>? ReplayPositionChanged
    {
        add => _PlaybackController.ReplayPositionChanged += value;
        remove => _PlaybackController.ReplayPositionChanged -= value;
    }

    public async Task StartLiveAsync(RecorderConnection connection)
    {
        await _Operations.WaitAsync();

        try
        {
            PlaybackRequest playbackRequest = _PlaybackSourceFactory.CreateStreamSource(connection);
            await _PlaybackController.StopAsync();
            await _PlaybackController.StartAsync(playbackRequest);
        }
        finally
        {
            _Operations.Release();
        }
    }

    public async Task<bool> StartReplayAsync(
        PlaybackRequest playbackRequest,
        Func<bool> canStart,
        Action beforeStart,
        CancellationToken ct)
    {
        await _Operations.WaitAsync(ct);

        try
        {
            if (!canStart())
            {
                return false;
            }

            beforeStart();
            await _PlaybackController.StartAsync(playbackRequest);
            return true;
        }
        finally
        {
            _Operations.Release();
        }
    }

    public async Task StopAsync()
    {
        await _Operations.WaitAsync();

        try
        {
            await _PlaybackController.StopAsync();
        }
        finally
        {
            _Operations.Release();
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _Operations.WaitAsync(ct);

        try
        {
            await _PlaybackController.StopAsync();
        }
        finally
        {
            _Operations.Release();
        }
    }

    public void TryStopIgnoringErrors()
    {
        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Interlocked.Exchange(ref _Disposed, 1) != 0)
        {
            return;
        }

        _Operations.Dispose();
    }
}
