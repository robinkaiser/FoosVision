// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Handshake;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Playback;

public class RtpPlaybackSourceFactoryTests
{
    [Fact]
    public void CreateStreamSource_maps_live_video_settings_from_connection()
    {
        FakeWritableSessionFile streamSdpFile = new("cache://stream.sdp");
        RtpPlaybackSourceFactory testee = new(streamSdpFile);
        RecorderConnection connection = new(
            RecorderIpAddress: "192.168.178.10",
            RecorderAppVersion: "1.2.3",
            ProtocolVersion: ProtocolVersions.Current,
            Diagnostics: new HandshakeDiagnosticsSettings(),
            Viewer: new HandshakeViewerSettings
            {
                LiveVideo = new HandshakeViewerLiveVideoSettings
                {
                    PlaybackBufferMilliseconds = 50,
                    MaxPlaybackBufferMilliseconds = 200,
                    DecoderLowLatency = false,
                    UdpReceiveBufferBytes = 1048576,
                },
            });

        PlaybackRequest result = testee.CreateStreamSource(connection);

        Assert.Equal("cache://stream.sdp", result.MediaSource);
        Assert.Equal(PlaybackKind.LiveStream, result.Kind);
        Assert.NotNull(result.LiveVideo);
        Assert.Equal(50, result.LiveVideo.PlaybackBufferMilliseconds);
        Assert.Equal(200, result.LiveVideo.MaxPlaybackBufferMilliseconds);
        Assert.False(result.LiveVideo.DecoderLowLatency);
        Assert.Equal(1048576, result.LiveVideo.UdpReceiveBufferBytes);
        Assert.Contains($"m=video {DefaultPorts.RtpH264StreamUdp} RTP/AVP 96", streamSdpFile.Content);
    }

    private sealed class FakeWritableSessionFile : IWritableSessionFile
    {
        public FakeWritableSessionFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public string Content { get; private set; } = string.Empty;

        public void WriteAllText(string content)
        {
            Content = content;
        }
    }
}
