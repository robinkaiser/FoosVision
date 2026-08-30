// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;

namespace FoosVision.Adapters.Viewer.Session.Playback;

public interface IPlaybackSourceFactory
{
    PlaybackRequest CreateStreamSource(RecorderConnection connection);
}
