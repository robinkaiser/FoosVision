// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Viewer.Session.Playback;

internal static class ReplayPlaybackRequestMapper
{
    private const double _ReplaySpeed = 0.25D;

    public static bool TryMap(ReplayMessage message, out PlaybackRequest playbackRequest, out string rejectionReason)
    {
        ArgumentNullException.ThrowIfNull(message);

        playbackRequest = default;
        rejectionReason = string.Empty;

        PlaybackCodec codec = MapCodec(message.Codec);
        if (codec != PlaybackCodec.H264)
        {
            rejectionReason = $"Replay codec '{message.Codec}' is not supported.";
            return false;
        }

        if (message.AccessUnits.Count == 0)
        {
            rejectionReason = "Replay contains no access units.";
            return false;
        }

        EncodedReplayPlayback replay = new(
            codec,
            message.ReplayStartTimestampNs,
            message.ReplayEndTimestampNs,
            [.. message.ParameterSets.Select(MapParameterSet)],
            [.. message.AccessUnits.Select(MapAccessUnit)],
            _ReplaySpeed);

        playbackRequest = new PlaybackRequest(string.Empty, PlaybackKind.EncodedReplay, replay);
        return true;
    }

    private static PlaybackCodec MapCodec(EncodedReplayCodecMessage codec)
        => codec switch
        {
            EncodedReplayCodecMessage.H264 => PlaybackCodec.H264,
            EncodedReplayCodecMessage.H265 => PlaybackCodec.H265,
            _ => PlaybackCodec.Unknown,
        };

    private static PlaybackParameterSet MapParameterSet(EncodedReplayParameterSetMessage parameterSet)
        => new(MapParameterSetType(parameterSet.Type), parameterSet.Buffer);

    private static PlaybackParameterSetType MapParameterSetType(EncodedReplayParameterSetTypeMessage parameterSetType)
        => parameterSetType switch
        {
            EncodedReplayParameterSetTypeMessage.VPS => PlaybackParameterSetType.VPS,
            EncodedReplayParameterSetTypeMessage.SPS => PlaybackParameterSetType.SPS,
            EncodedReplayParameterSetTypeMessage.PPS => PlaybackParameterSetType.PPS,
            _ => PlaybackParameterSetType.Invalid,
        };

    private static PlaybackAccessUnit MapAccessUnit(EncodedReplayAccessUnitMessage accessUnit)
        => new(
            accessUnit.TimeNs,
            accessUnit.IsKeyFrame,
            accessUnit.ContainsAllRequiredParameterSets,
            accessUnit.Buffer);
}
