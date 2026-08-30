// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using System.Text;
using FFmpeg.AutoGen;
using FoosVision.Common.Logging;
using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Windows.FileCapture.Mp4;

internal unsafe class FfmpegMp4AccessUnitSource : IMp4AccessUnitSource
{
    private static readonly Source _Log = new("Media.Windows.FfmpegMp4AccessUnitSource");

    private AVFormatContext* _FormatContext;
    private AVPacket* _ReadPacket;
    private int _VideoStreamIndex = -1;
    private bool _IsConfigured;
    private bool _Disposed;
    private Mp4AnnexBConverter? _AnnexBConverter;

    public Mp4VideoStreamInfo StreamInfo { get; private set; } = new(CodecType.Unknown, 0, 0);

    public void Configure(string filePath)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must be specified.", nameof(filePath));
        }

        DisposeNativeState();

        AVFormatContext* formatContext = null;
        _Log.Information("Opening MP4 file '{0}' via FFmpeg.", filePath);
        int openResult = ffmpeg.avformat_open_input(&formatContext, filePath, null, null);
        ThrowOnError(openResult, $"Failed to open MP4 file '{filePath}'.");
        _FormatContext = formatContext;

        int streamInfoResult = ffmpeg.avformat_find_stream_info(_FormatContext, null);
        ThrowOnError(streamInfoResult, "Failed to read MP4 stream information.");

        _VideoStreamIndex = ffmpeg.av_find_best_stream(_FormatContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, null, 0);
        if (_VideoStreamIndex < 0)
        {
            throw new InvalidOperationException("No video stream found in MP4 file.");
        }

        AVStream* stream = _FormatContext->streams[_VideoStreamIndex];
        AVCodecParameters* codecParameters = stream->codecpar;
        CodecType codec = Mp4CodecMappings.ResolveCodecType(codecParameters->codec_id);
        if (codec == CodecType.Unknown)
        {
            throw new NotSupportedException($"Codec '{codecParameters->codec_id}' is not supported for MP4 playback.");
        }

        ReadOnlySpan<byte> extraData = codecParameters->extradata == null || codecParameters->extradata_size <= 0
            ? []
            : new ReadOnlySpan<byte>(codecParameters->extradata, codecParameters->extradata_size);

        _AnnexBConverter = new Mp4AnnexBConverter(codec, extraData);
        StreamInfo = new Mp4VideoStreamInfo(codec, codecParameters->width, codecParameters->height);

        _ReadPacket = ffmpeg.av_packet_alloc();
        if (_ReadPacket == null)
        {
            throw new InvalidOperationException("Failed to allocate FFmpeg packet buffer.");
        }

        _IsConfigured = true;
        _Log.Information("Opened MP4 file '{0}'. Codec: {1}, size: {2}x{3}, stream index: {4}.", filePath, codec, codecParameters->width, codecParameters->height, _VideoStreamIndex);
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        if (!_IsConfigured || _FormatContext == null)
        {
            throw new InvalidOperationException("Access unit source is not configured.");
        }

        int seekResult = ffmpeg.av_seek_frame(_FormatContext, _VideoStreamIndex, 0, ffmpeg.AVSEEK_FLAG_BACKWARD);
        ThrowOnError(seekResult, "Failed to seek MP4 stream to the beginning.");

        if (_ReadPacket != null)
        {
            ffmpeg.av_packet_unref(_ReadPacket);
        }
    }

    public bool TryReadNextAccessUnit([NotNullWhen(true)] out Mp4AccessUnit? accessUnit)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        if (!_IsConfigured || _FormatContext == null || _ReadPacket == null || _AnnexBConverter == null)
        {
            throw new InvalidOperationException("Access unit source is not configured.");
        }

        while (true)
        {
            int readResult = ffmpeg.av_read_frame(_FormatContext, _ReadPacket);
            if (readResult == ffmpeg.AVERROR_EOF)
            {
                accessUnit = null;
                return false;
            }

            ThrowOnError(readResult, "Failed to read MP4 packet.");

            if (_ReadPacket->stream_index != _VideoStreamIndex)
            {
                ffmpeg.av_packet_unref(_ReadPacket);
                continue;
            }

            accessUnit = CreateAccessUnit(_ReadPacket);
            ffmpeg.av_packet_unref(_ReadPacket);
            return true;
        }
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        DisposeNativeState();
        GC.SuppressFinalize(this);
    }

    private Mp4AccessUnit CreateAccessUnit(AVPacket* packet)
    {
        long pts = packet->pts != ffmpeg.AV_NOPTS_VALUE ? packet->pts : packet->dts;
        long timestampNs = pts == ffmpeg.AV_NOPTS_VALUE
            ? 0
            : RescaleToNanoseconds(pts, _FormatContext->streams[_VideoStreamIndex]->time_base);
        bool isKeyFrame = (packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0;

        byte[] buffer;
        if (packet->size <= 0)
        {
            buffer = _AnnexBConverter!.ConvertPacket([], isKeyFrame);
        }
        else
        {
            ReadOnlySpan<byte> packetData = new(packet->data, packet->size);
            buffer = _AnnexBConverter!.ConvertPacket(packetData, isKeyFrame);
        }

        return new Mp4AccessUnit(timestampNs, isKeyFrame, buffer);
    }

    private static long RescaleToNanoseconds(long timestamp, AVRational timeBase)
    {
        return ffmpeg.av_rescale_q(timestamp, timeBase, new AVRational { num = 1, den = 1_000_000_000 });
    }

    private void DisposeNativeState()
    {
        if (_ReadPacket != null)
        {
            AVPacket* packet = _ReadPacket;
            ffmpeg.av_packet_free(&packet);
            _ReadPacket = null;
        }

        if (_FormatContext != null)
        {
            AVFormatContext* context = _FormatContext;
            ffmpeg.avformat_close_input(&context);
            _FormatContext = null;
        }

        _AnnexBConverter = null;
        _VideoStreamIndex = -1;
        _IsConfigured = false;
        StreamInfo = new Mp4VideoStreamInfo(CodecType.Unknown, 0, 0);
    }

    private static void ThrowOnError(int result, string message)
    {
        if (result >= 0)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[1024];
        fixed (byte* bufferPointer = buffer)
        {
            ffmpeg.av_strerror(result, bufferPointer, (ulong)buffer.Length);
        }

        int zeroIndex = buffer.IndexOf((byte)0);
        string detail = zeroIndex >= 0
            ? Encoding.UTF8.GetString(buffer[..zeroIndex])
            : Encoding.UTF8.GetString(buffer);
        throw new InvalidOperationException($"{message} FFmpeg error {result}: {detail}");
    }
}
