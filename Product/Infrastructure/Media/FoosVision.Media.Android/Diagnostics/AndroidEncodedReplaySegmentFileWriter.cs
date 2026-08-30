// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Media;
using FoosVision.Ports.Media;
using Java.Nio;

namespace FoosVision.Media.Android.Diagnostics;

public class AndroidEncodedReplaySegmentFileWriter : IEncodedReplaySegmentFileWriter
{
    private const int _DefaultWidth = 1920;
    private const int _DefaultHeight = 1080;

    private readonly int _Width;
    private readonly int _Height;

    public AndroidEncodedReplaySegmentFileWriter()
        : this(_DefaultWidth, _DefaultHeight)
    {
    }

    public AndroidEncodedReplaySegmentFileWriter(int width, int height)
    {
        _Width = width;
        _Height = height;
    }

    public Task WriteAsync(EncodedReplaySegment segment, string filePath, CancellationToken ct)
    {
        ValidateSegment(segment);

        using MediaMuxer muxer = new(filePath, MuxerOutputType.Mpeg4);
        using MediaFormat format = CreateFormat(segment);

        int trackIndex = muxer.AddTrack(format);
        muxer.Start();

        bool stopped = false;

        try
        {
            WriteSamples(muxer, trackIndex, segment, ct);
            muxer.Stop();
            stopped = true;
        }
        finally
        {
            if (!stopped)
            {
                TryStop(muxer);
            }
        }

        return Task.CompletedTask;
    }

    private MediaFormat CreateFormat(EncodedReplaySegment segment)
    {
        string mimeType = segment.Codec switch
        {
            EncodedReplayCodec.H264 => MediaFormat.MimetypeVideoAvc,
            EncodedReplayCodec.H265 => MediaFormat.MimetypeVideoHevc,
            _ => throw new InvalidOperationException($"Unsupported encoded replay codec '{segment.Codec}'."),
        };

        MediaFormat format = MediaFormat.CreateVideoFormat(mimeType, _Width, _Height);

        if (segment.Codec == EncodedReplayCodec.H264)
        {
            SetCsd(format, "csd-0", segment.ParameterSets, EncodedReplayParameterSetType.SPS);
            SetCsd(format, "csd-1", segment.ParameterSets, EncodedReplayParameterSetType.PPS);
        }
        else
        {
            byte[] hevcCsd = CombineParameterSets(
                segment.ParameterSets,
                EncodedReplayParameterSetType.VPS,
                EncodedReplayParameterSetType.SPS,
                EncodedReplayParameterSetType.PPS);
            format.SetByteBuffer("csd-0", ByteBuffer.Wrap(hevcCsd));
        }

        return format;
    }

    private static void WriteSamples(
        MediaMuxer muxer,
        int trackIndex,
        EncodedReplaySegment segment,
        CancellationToken ct)
    {
        long firstTimestampNs = segment.AccessUnits[0].TimeNs;
        long lastPresentationTimeUs = -1;

        foreach (EncodedReplayAccessUnit accessUnit in segment.AccessUnits)
        {
            ct.ThrowIfCancellationRequested();

            long presentationTimeUs = (accessUnit.TimeNs - firstTimestampNs) / 1000L;

            if (presentationTimeUs < lastPresentationTimeUs)
            {
                throw new InvalidOperationException(
                    $"Encoded replay timestamps are not monotone. PreviousUs={lastPresentationTimeUs}, CurrentUs={presentationTimeUs}.");
            }

            MediaCodecBufferFlags flags = accessUnit.IsKeyFrame
                ? MediaCodecBufferFlags.SyncFrame
                : MediaCodecBufferFlags.None;

            using ByteBuffer buffer = ByteBuffer.Wrap(accessUnit.Buffer);
            MediaCodec.BufferInfo bufferInfo = new();
            bufferInfo.Set(0, accessUnit.Buffer.Length, presentationTimeUs, flags);
            muxer.WriteSampleData(trackIndex, buffer, bufferInfo);

            lastPresentationTimeUs = presentationTimeUs;
        }
    }

    private static void ValidateSegment(EncodedReplaySegment segment)
    {
        if (segment.Codec is not EncodedReplayCodec.H264 and not EncodedReplayCodec.H265)
        {
            throw new InvalidOperationException($"Unsupported encoded replay codec '{segment.Codec}'.");
        }

        if (segment.AccessUnits.Count == 0)
        {
            throw new InvalidOperationException("Encoded replay segment contains no access units.");
        }

        if (!segment.AccessUnits[0].IsKeyFrame)
        {
            throw new InvalidOperationException("Encoded replay segment must start at a keyframe.");
        }

        if (segment.AccessUnits.Any(x => x.Buffer.Length == 0))
        {
            throw new InvalidOperationException("Encoded replay segment contains an empty access unit.");
        }

        if (segment.Codec == EncodedReplayCodec.H264)
        {
            EnsureParameterSet(segment, EncodedReplayParameterSetType.SPS);
            EnsureParameterSet(segment, EncodedReplayParameterSetType.PPS);
            return;
        }

        EnsureParameterSet(segment, EncodedReplayParameterSetType.VPS);
        EnsureParameterSet(segment, EncodedReplayParameterSetType.SPS);
        EnsureParameterSet(segment, EncodedReplayParameterSetType.PPS);
    }

    private static void EnsureParameterSet(EncodedReplaySegment segment, EncodedReplayParameterSetType type)
    {
        if (!segment.ParameterSets.Any(x => x.Type == type && x.Buffer.Length > 0))
        {
            throw new InvalidOperationException($"Encoded replay segment is missing required parameter set '{type}'.");
        }
    }

    private static void SetCsd(
        MediaFormat format,
        string key,
        IReadOnlyList<EncodedReplayParameterSet> parameterSets,
        EncodedReplayParameterSetType type)
    {
        EncodedReplayParameterSet parameterSet = parameterSets.First(x => x.Type == type);
        format.SetByteBuffer(key, ByteBuffer.Wrap(parameterSet.Buffer));
    }

    private static byte[] CombineParameterSets(
        IReadOnlyList<EncodedReplayParameterSet> parameterSets,
        params EncodedReplayParameterSetType[] types)
    {
        int length = types.Sum(type => parameterSets.First(x => x.Type == type).Buffer.Length);
        byte[] combined = new byte[length];
        int offset = 0;

        foreach (EncodedReplayParameterSetType type in types)
        {
            byte[] buffer = parameterSets.First(x => x.Type == type).Buffer;
            buffer.CopyTo(combined, offset);
            offset += buffer.Length;
        }

        return combined;
    }

    private static void TryStop(MediaMuxer muxer)
    {
        try
        {
            muxer.Stop();
        }
        catch
        {
        }
    }
}
