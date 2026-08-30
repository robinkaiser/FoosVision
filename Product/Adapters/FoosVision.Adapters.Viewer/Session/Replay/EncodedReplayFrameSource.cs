// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Viewer.Session.Replay;

public class EncodedReplayFrameSource
{
    private readonly IEncodedReplayFrameDecoder _Decoder;
    private readonly EncodedReplayPlayback _Replay;

    public EncodedReplayFrameSource(IEncodedReplayFrameDecoder decoder, EncodedReplayPlayback replay)
    {
        _Decoder = decoder;
        _Replay = replay;
    }

    public async IAsyncEnumerable<ReplayFrame> ReadFrames(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var request = new EncodedReplayDecodeRequest(
            MapCodec(_Replay.Codec),
            [.. _Replay.ParameterSets.Select(MapParameterSet)],
            [.. _Replay.AccessUnits.Select(MapAccessUnit)]);

        await foreach (DecodedReplayFrame frame in _Decoder.Decode(request, ct).ConfigureAwait(false))
        {
            yield return new ReplayFrame(frame.Frame);
        }
    }

    private static EncodedReplayCodec MapCodec(PlaybackCodec codec)
        => codec switch
        {
            PlaybackCodec.H264 => EncodedReplayCodec.H264,
            PlaybackCodec.H265 => EncodedReplayCodec.H265,
            _ => EncodedReplayCodec.Unknown,
        };

    private static EncodedReplayParameterSet MapParameterSet(PlaybackParameterSet parameterSet)
        => new(MapParameterSetType(parameterSet.Type), parameterSet.Buffer);

    private static EncodedReplayAccessUnit MapAccessUnit(PlaybackAccessUnit accessUnit)
        => new(
            accessUnit.TimeNs,
            accessUnit.IsKeyFrame,
            accessUnit.ContainsAllRequiredParameterSets,
            accessUnit.Buffer);

    private static EncodedReplayParameterSetType MapParameterSetType(PlaybackParameterSetType type)
        => type switch
        {
            PlaybackParameterSetType.VPS => EncodedReplayParameterSetType.VPS,
            PlaybackParameterSetType.SPS => EncodedReplayParameterSetType.SPS,
            PlaybackParameterSetType.PPS => EncodedReplayParameterSetType.PPS,
            _ => EncodedReplayParameterSetType.Invalid,
        };
}
