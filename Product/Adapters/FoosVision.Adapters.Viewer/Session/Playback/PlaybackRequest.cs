// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Session.Playback;

public enum PlaybackKind
{
    LiveStream = 0,
    EncodedReplay = 1,
}

public enum PlaybackCodec
{
    Unknown = 0,
    H264 = 1,
    H265 = 2,
}

public enum PlaybackParameterSetType
{
    Invalid = 0,
    VPS = 1,
    SPS = 2,
    PPS = 3,
}

public readonly record struct PlaybackParameterSet(
    PlaybackParameterSetType Type,
    byte[] Buffer);

public readonly record struct PlaybackAccessUnit(
    long TimeNs,
    bool IsKeyFrame,
    bool ContainsAllRequiredParameterSets,
    byte[] Buffer);

public record EncodedReplayPlayback(
    PlaybackCodec Codec,
    long ReplayStartTimestampNs,
    long ReplayEndTimestampNs,
    IReadOnlyList<PlaybackParameterSet> ParameterSets,
    IReadOnlyList<PlaybackAccessUnit> AccessUnits,
    double Speed);

public record LiveVideoPlaybackOptions(
    int PlaybackBufferMilliseconds = 25,
    int MaxPlaybackBufferMilliseconds = 100,
    bool DecoderLowLatency = true,
    int UdpReceiveBufferBytes = 2 * 1024 * 1024)
{
    public static LiveVideoPlaybackOptions Default { get; } = new();
}

public readonly record struct PlaybackRequest(
    string MediaSource,
    PlaybackKind Kind,
    EncodedReplayPlayback? EncodedReplay = null,
    LiveVideoPlaybackOptions? LiveVideo = null);
