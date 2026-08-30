// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Android.Decoding;

public class AndroidEncodedReplayFrameDecoder : IEncodedReplayFrameDecoder
{
    private const int _ReplayWidth = 1920;
    private const int _ReplayHeight = 1080;
    private const int _ReplayYuvFramePoolSize = 32;

    public async IAsyncEnumerable<DecodedReplayFrame> Decode(
        EncodedReplayDecodeRequest replay,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(replay);

        using AndroidYuvVideoDecoder decoder = new(_ReplayYuvFramePoolSize);
        decoder.Configure(new AndroidVideoDecoderOptions(MapCodec(replay.Codec), _ReplayWidth, _ReplayHeight));

        foreach (EncodedReplayAccessUnit accessUnit in replay.AccessUnits)
        {
            ct.ThrowIfCancellationRequested();
            decoder.PushAccessUnit(PrepareAccessUnit(replay, accessUnit), accessUnit.TimeNs, accessUnit.IsKeyFrame);

            foreach (DecodedReplayFrame frame in DrainDecodedFrames(decoder))
            {
                yield return frame;
            }

            await Task.Yield();
        }

        ct.ThrowIfCancellationRequested();
        decoder.Flush();

        await foreach (DecodedReplayFrame frame in DrainRemainingDecodedFrames(decoder, ct).ConfigureAwait(false))
        {
            yield return frame;
        }
    }

    private static IEnumerable<DecodedReplayFrame> DrainDecodedFrames(AndroidYuvVideoDecoder decoder)
    {
        while (decoder.TryDequeueFrame(out AndroidYuvDecodedFrame? frame))
        {
            yield return new DecodedReplayFrame(frame);
        }
    }

    private static async IAsyncEnumerable<DecodedReplayFrame> DrainRemainingDecodedFrames(
        AndroidYuvVideoDecoder decoder,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!decoder.IsEndOfStreamDrained)
        {
            ct.ThrowIfCancellationRequested();

            bool drainedFrame = false;
            foreach (DecodedReplayFrame frame in DrainDecodedFrames(decoder))
            {
                drainedFrame = true;
                yield return frame;
            }

            if (!drainedFrame)
            {
                await Task.Yield();
            }
        }
    }

    private static CodecType MapCodec(EncodedReplayCodec codec)
        => codec switch
        {
            EncodedReplayCodec.H264 => CodecType.H264,
            EncodedReplayCodec.H265 => CodecType.H265,
            _ => throw new NotSupportedException($"Replay codec '{codec}' is not supported."),
        };

    private static byte[] PrepareAccessUnit(EncodedReplayDecodeRequest replay, EncodedReplayAccessUnit accessUnit)
    {
        if (!accessUnit.IsKeyFrame || accessUnit.ContainsAllRequiredParameterSets || replay.ParameterSets.Count == 0)
        {
            return accessUnit.Buffer;
        }

        IReadOnlyList<EncodedReplayParameterSetType> requiredTypes = replay.Codec == EncodedReplayCodec.H264
            ? [EncodedReplayParameterSetType.SPS, EncodedReplayParameterSetType.PPS]
            : [EncodedReplayParameterSetType.VPS, EncodedReplayParameterSetType.SPS, EncodedReplayParameterSetType.PPS];

        List<byte[]> parameterSetBuffers = [];
        foreach (EncodedReplayParameterSetType requiredType in requiredTypes)
        {
            EncodedReplayParameterSet? parameterSet = replay.ParameterSets.LastOrDefault(p => p.Type == requiredType);
            if (parameterSet?.Buffer is { Length: > 0 })
            {
                parameterSetBuffers.Add(parameterSet.Buffer);
            }
        }

        if (parameterSetBuffers.Count == 0)
        {
            return accessUnit.Buffer;
        }

        int length = accessUnit.Buffer.Length + parameterSetBuffers.Sum(p => p.Length);
        byte[] buffer = new byte[length];
        int offset = 0;
        foreach (byte[] parameterSet in parameterSetBuffers)
        {
            parameterSet.CopyTo(buffer, offset);
            offset += parameterSet.Length;
        }

        accessUnit.Buffer.CopyTo(buffer, offset);
        return buffer;
    }
}
