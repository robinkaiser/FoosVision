// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Handshake;

[MessagePackObject(true)]
public record HandshakeViewerSettings
{
    public HandshakeViewerLiveVideoSettings LiveVideo { get; init; } = new();
}

[MessagePackObject(true)]
public record HandshakeViewerLiveVideoSettings
{
    public int PlaybackBufferMilliseconds { get; init; } = 25;

    public int MaxPlaybackBufferMilliseconds { get; init; } = 100;

    public bool DecoderLowLatency { get; init; } = true;

    public int UdpReceiveBufferBytes { get; init; } = 2 * 1024 * 1024;
}
