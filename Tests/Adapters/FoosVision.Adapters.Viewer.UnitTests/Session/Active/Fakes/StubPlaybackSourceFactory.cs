// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session.Playback;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class StubPlaybackSourceFactory : IPlaybackSourceFactory
{
    public PlaybackRequest CreateStreamSource(RecorderConnection connection)
    {
        Assert.Equal("192.168.178.10", connection.RecorderIpAddress);
        return new PlaybackRequest("cache://stream.sdp", PlaybackKind.LiveStream);
    }
}
