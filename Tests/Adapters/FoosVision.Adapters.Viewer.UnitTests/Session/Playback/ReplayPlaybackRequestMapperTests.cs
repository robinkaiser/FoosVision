// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Playback;

public class ReplayPlaybackRequestMapperTests
{
    [Fact]
    public void TryMap_maps_h264_goal_check_to_encoded_replay_request()
    {
        ReplayClipMessage message = CreateReplayClipMessage();

        bool result = ReplayPlaybackRequestMapper.TryMap(message, out PlaybackRequest request, out string reason);

        Assert.True(result);
        Assert.Equal(string.Empty, reason);
        Assert.Equal(PlaybackKind.EncodedReplay, request.Kind);
        Assert.NotNull(request.EncodedReplay);
        Assert.Equal(PlaybackCodec.H264, request.EncodedReplay.Codec);
        Assert.Equal(1_900_000_000, request.EncodedReplay.ReplayStartTimestampNs);
        Assert.Equal(2_900_000_000, request.EncodedReplay.ReplayEndTimestampNs);
        Assert.Equal(0.25D, request.EncodedReplay.Speed);
        Assert.Single(request.EncodedReplay.ParameterSets);
        Assert.Single(request.EncodedReplay.AccessUnits);
    }

    [Theory]
    [InlineData(EncodedReplayCodecMessage.Unknown)]
    [InlineData(EncodedReplayCodecMessage.H265)]
    public void TryMap_rejects_unsupported_codecs(EncodedReplayCodecMessage codec)
    {
        ReplayClipMessage message = CreateReplayClipMessage() with
        {
            Codec = codec,
        };

        bool result = ReplayPlaybackRequestMapper.TryMap(message, out PlaybackRequest request, out string reason);

        Assert.False(result);
        Assert.Equal(default, request);
        Assert.Contains("not supported", reason);
    }

    [Fact]
    public void TryMap_rejects_empty_replay()
    {
        ReplayClipMessage message = CreateReplayClipMessage() with
        {
            AccessUnits = [],
        };

        bool result = ReplayPlaybackRequestMapper.TryMap(message, out PlaybackRequest request, out string reason);

        Assert.False(result);
        Assert.Equal(default, request);
        Assert.Equal("Replay contains no access units.", reason);
    }

    [Fact]
    public void TryMap_uses_recorder_speed()
    {
        Assert.True(ReplayPlaybackRequestMapper.TryMap(CreateReplayClipMessage() with { Speed = 0.5 }, out PlaybackRequest request, out _));
        Assert.Equal(0.5, request.EncodedReplay!.Speed);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(0.25, 0)]
    public void TryMap_rejects_invalid_playback_settings(double speed, int loops)
    {
        Assert.False(ReplayPlaybackRequestMapper.TryMap(CreateReplayClipMessage() with
        {
            Speed = speed, RequiredCompletedLoops = loops,
        }, out _, out _));
    }

    private static ReplayClipMessage CreateReplayClipMessage()
    {
        return new ReplayClipMessage
        {
            ReplayStartTimestampNs = 1_900_000_000,
            ReplayEndTimestampNs = 2_900_000_000,
            Codec = EncodedReplayCodecMessage.H264,
            ParameterSets =
            [
                new EncodedReplayParameterSetMessage
                {
                    Type = EncodedReplayParameterSetTypeMessage.SPS,
                    Buffer = [0, 0, 0, 1, 0x67],
                },
            ],
            AccessUnits =
            [
                new EncodedReplayAccessUnitMessage
                {
                    TimeNs = 1_900_000_000,
                    IsKeyFrame = true,
                    ContainsAllRequiredParameterSets = true,
                    Buffer = [0, 0, 0, 1, 0x65],
                },
            ],
        };
    }
}
