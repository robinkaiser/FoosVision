// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Session.Playback;

public interface IPlaybackController
{
    event Func<Task>? ReplayLoopCompleted;

    event Func<long, Task>? ReplayPositionChanged;

    Task StartAsync(PlaybackRequest playbackRequest);

    Task StopAsync();
}
