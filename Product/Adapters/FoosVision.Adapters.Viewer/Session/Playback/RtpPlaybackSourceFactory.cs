// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Messages.Handshake;

namespace FoosVision.Adapters.Viewer.Session.Playback;

public class RtpPlaybackSourceFactory : IPlaybackSourceFactory
{
    private readonly IWritableSessionFile _StreamSdpFile;

    public RtpPlaybackSourceFactory(IWritableSessionFile streamSdpFile)
    {
        _StreamSdpFile = streamSdpFile;
    }

    public PlaybackRequest CreateStreamSource(RecorderConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        _StreamSdpFile.WriteAllText(CreateRtpSdpContent());
        HandshakeViewerLiveVideoSettings liveVideo = connection.Viewer.LiveVideo;

        return new PlaybackRequest(
            _StreamSdpFile.Path,
            PlaybackKind.LiveStream,
            LiveVideo: new LiveVideoPlaybackOptions(
                liveVideo.PlaybackBufferMilliseconds,
                liveVideo.MaxPlaybackBufferMilliseconds,
                liveVideo.DecoderLowLatency,
                liveVideo.UdpReceiveBufferBytes));
    }

    private static string CreateRtpSdpContent()
    {
        return
            "v=0\n" +
            "o=- 0 0 IN IP4 127.0.0.1\n" +
            "s=FoosVision RTP H264\n" +
            "c=IN IP4 0.0.0.0\n" +
            "t=0 0\n" +
            $"m=video {DefaultPorts.RtpH264StreamUdp} RTP/AVP 96\n" +
            "a=rtpmap:96 H264/90000\n" +
            "a=fmtp:96 packetization-mode=1\n" +
            "a=recvonly\n";
    }
}
