// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Protocol.Messages.Handshake;
using FoosVision.Settings;

namespace FoosVision.VideoPlayer.Runtime;

public class VideoPlayerHandshakeViewerSettingsProvider : IHandshakeViewerSettingsProvider
{
    public HandshakeViewerSettings GetViewerSettings()
    {
        ViewerLiveVideoSettings? liveVideo = VideoPlayerLoggingBootstrap.CurrentSettings?.Settings.Viewer.LiveVideo;

        if (liveVideo is null)
        {
            return new HandshakeViewerSettings();
        }

        return new HandshakeViewerSettings
        {
            LiveVideo = new HandshakeViewerLiveVideoSettings
            {
                PlaybackBufferMilliseconds = liveVideo.PlaybackBufferMilliseconds,
                MaxPlaybackBufferMilliseconds = liveVideo.MaxPlaybackBufferMilliseconds,
                DecoderLowLatency = liveVideo.DecoderLowLatency,
                UdpReceiveBufferBytes = liveVideo.UdpReceiveBufferBytes,
            },
        };
    }
}
