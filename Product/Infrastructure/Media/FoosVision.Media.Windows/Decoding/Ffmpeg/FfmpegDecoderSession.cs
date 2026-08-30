// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;
using FFmpeg.AutoGen;
using FoosVision.Common.Logging;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Windows.Decoding.Ffmpeg;

internal unsafe class FfmpegDecoderSession : IFfmpegDecoderSession
{
    private const int _SwsFastBilinear = 1;

    private static readonly Source _Log = new("Media.Windows.FfmpegDecoderSession");

    private readonly Queue<FfmpegDecodedFrame> _DecodedFrames = [];
    private readonly FfmpegHardwareDecodeConfig? _HardwareDecodeConfig;
    private readonly AVCodecContext_get_format _GetFormatCallback;

    private AVCodecContext* _CodecContext;
    private AVPacket* _Packet;
    private AVFrame* _DecodeFrame;
    private AVFrame* _TransferFrame;
    private SwsContext* _ScaleContext;
    private int _ScaleContextSourceWidth;
    private int _ScaleContextSourceHeight;
    private AVPixelFormat _ScaleContextSourcePixelFormat = AVPixelFormat.AV_PIX_FMT_NONE;
    private AVBufferRef* _HardwareDeviceContext;
    private GCHandle _Handle;
    private FfmpegDecoderOptions? _Options;
    private bool _Disposed;

    public FfmpegDecoderSession()
        : this(null)
    {
    }

    internal FfmpegDecoderSession(FfmpegHardwareDecodeConfig? hardwareDecodeConfig)
    {
        _HardwareDecodeConfig = hardwareDecodeConfig;
        _GetFormatCallback = GetHardwarePixelFormat;
    }

    public bool IsConfigured => _CodecContext != null;

    public bool IsHardwareAccelerated => _HardwareDecodeConfig.HasValue;

    public void Configure(FfmpegDecoderOptions options)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        Reset();
        DisposeNativeState();
        _Options = options;

        AVCodecID codecId = options.Codec switch
        {
            CodecType.H264 => AVCodecID.AV_CODEC_ID_H264,
            CodecType.H265 => AVCodecID.AV_CODEC_ID_HEVC,
            _ => throw new NotSupportedException($"Codec '{options.Codec}' is not supported."),
        };

        AVCodec* codec = ffmpeg.avcodec_find_decoder(codecId);
        if (codec == null)
        {
            throw new InvalidOperationException($"FFmpeg decoder not found for codec '{options.Codec}'.");
        }

        _CodecContext = ffmpeg.avcodec_alloc_context3(codec);
        if (_CodecContext == null)
        {
            throw new InvalidOperationException("Failed to allocate FFmpeg codec context.");
        }

        _CodecContext->width = options.Width;
        _CodecContext->height = options.Height;
        _CodecContext->thread_count = 0;
        _CodecContext->pkt_timebase = new AVRational
        {
            num = 1,
            den = 1_000_000_000,
        };

        if (_HardwareDecodeConfig.HasValue)
        {
            InitializeHardwareDecodeContext();
        }

        int result = ffmpeg.avcodec_open2(_CodecContext, codec, null);
        ThrowOnError(result, "Failed to open FFmpeg decoder.");

        _Packet = ffmpeg.av_packet_alloc();
        if (_Packet == null)
        {
            throw new InvalidOperationException("Failed to allocate FFmpeg packet.");
        }

        _DecodeFrame = ffmpeg.av_frame_alloc();
        if (_DecodeFrame == null)
        {
            throw new InvalidOperationException("Failed to allocate FFmpeg frame.");
        }

        _TransferFrame = ffmpeg.av_frame_alloc();
        if (_TransferFrame == null)
        {
            throw new InvalidOperationException("Failed to allocate FFmpeg transfer frame.");
        }

        _Log.Information(
            "Configured FFmpeg decoder session. Codec: {0}, size: {1}x{2}, hardware: {3}.",
            options.Codec,
            options.Width,
            options.Height,
            _HardwareDecodeConfig.HasValue);
    }

    public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool enqueueFrames = true)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        if (!IsConfigured)
        {
            throw new InvalidOperationException("Decoder session is not configured.");
        }

        if (buffer.IsEmpty)
        {
            throw new ArgumentException("Access unit must not be empty.", nameof(buffer));
        }

        ffmpeg.av_packet_unref(_Packet);

        int packetResult = ffmpeg.av_new_packet(_Packet, buffer.Length);
        ThrowOnError(packetResult, "Failed to allocate FFmpeg packet payload.");

        fixed (byte* source = buffer)
        {
            Buffer.MemoryCopy(source, _Packet->data, buffer.Length, buffer.Length);
        }

        _Packet->pts = timeNs;
        _Packet->dts = timeNs;
        _Packet->flags = isKeyFrame ? ffmpeg.AV_PKT_FLAG_KEY : 0;

        int sendResult = ffmpeg.avcodec_send_packet(_CodecContext, _Packet);
        ThrowOnError(sendResult, "Failed to submit FFmpeg packet.");
        ffmpeg.av_packet_unref(_Packet);

        DrainDecodedFrames(enqueueFrames);
    }

    public bool TryDequeueFrame([NotNullWhen(true)] out FfmpegDecodedFrame? frame)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        if (_DecodedFrames.Count == 0)
        {
            frame = null;
            return false;
        }

        frame = _DecodedFrames.Dequeue();
        return true;
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        if (!IsConfigured)
        {
            return;
        }

        int sendResult = ffmpeg.avcodec_send_packet(_CodecContext, null);
        if (sendResult != ffmpeg.AVERROR_EOF)
        {
            ThrowOnError(sendResult, "Failed to flush FFmpeg decoder.");
        }

        DrainDecodedFrames(enqueueFrames: true);
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        while (_DecodedFrames.Count != 0)
        {
            _DecodedFrames.Dequeue().Dispose();
        }

        if (_CodecContext != null)
        {
            ffmpeg.avcodec_flush_buffers(_CodecContext);
        }

        if (_TransferFrame != null)
        {
            ffmpeg.av_frame_unref(_TransferFrame);
        }

        ResetScaleContextState();
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        Reset();
        _Disposed = true;
        DisposeNativeState();
        GC.SuppressFinalize(this);
    }

    private void InitializeHardwareDecodeContext()
    {
        _Handle = GCHandle.Alloc(this);
        _CodecContext->opaque = (void*)GCHandle.ToIntPtr(_Handle);
        _CodecContext->get_format = _GetFormatCallback;

        FfmpegHardwareDecodeConfig hardwareDecodeConfig = _HardwareDecodeConfig!.Value;
        AVBufferRef* hardwareDeviceContext = null;
        int createResult = ffmpeg.av_hwdevice_ctx_create(
            &hardwareDeviceContext,
            hardwareDecodeConfig.DeviceType,
            null,
            null,
            0);
        ThrowOnError(createResult, $"Failed to create FFmpeg hardware device context for '{hardwareDecodeConfig.DeviceType}'.");

        _HardwareDeviceContext = hardwareDeviceContext;
        _CodecContext->hw_device_ctx = ffmpeg.av_buffer_ref(_HardwareDeviceContext);
        if (_CodecContext->hw_device_ctx == null)
        {
            throw new InvalidOperationException("Failed to reference FFmpeg hardware device context.");
        }

        _Log.Information("Initialized FFmpeg hardware decode context for device type '{0}'.", hardwareDecodeConfig.DeviceType);
    }

    private void DrainDecodedFrames(bool enqueueFrames)
    {
        while (true)
        {
            int receiveResult = ffmpeg.avcodec_receive_frame(_CodecContext, _DecodeFrame);
            if (receiveResult == ffmpeg.AVERROR(ffmpeg.EAGAIN) ||
                receiveResult == ffmpeg.AVERROR_EOF)
            {
                return;
            }

            ThrowOnError(receiveResult, "Failed to receive decoded FFmpeg frame.");

            if (enqueueFrames)
            {
                _DecodedFrames.Enqueue(ConvertFrame(_DecodeFrame));
            }

            ffmpeg.av_frame_unref(_DecodeFrame);
        }
    }

    private FfmpegDecodedFrame ConvertFrame(AVFrame* sourceFrame)
    {
        if (_Options == null)
        {
            throw new InvalidOperationException("Decoder session options are not available.");
        }

        long timestamp = sourceFrame->best_effort_timestamp == ffmpeg.AV_NOPTS_VALUE
            ? 0
            : sourceFrame->best_effort_timestamp;

        AVFrame* workingFrame = sourceFrame;
        AVPixelFormat sourcePixelFormat = (AVPixelFormat)sourceFrame->format;

        if (_HardwareDecodeConfig.HasValue && sourcePixelFormat == _HardwareDecodeConfig.Value.PixelFormat)
        {
            ffmpeg.av_frame_unref(_TransferFrame);
            int transferResult = ffmpeg.av_hwframe_transfer_data(_TransferFrame, sourceFrame, 0);
            ThrowOnError(transferResult, "Failed to transfer FFmpeg hardware frame to system memory.");
            workingFrame = _TransferFrame;
            sourcePixelFormat = (AVPixelFormat)workingFrame->format;
        }

        int width = workingFrame->width > 0 ? workingFrame->width : _Options.Width;
        int height = workingFrame->height > 0 ? workingFrame->height : _Options.Height;
        EnsureScaleContext(width, height, sourcePixelFormat);

        int stride = width * 4;
        int bufferLength = stride * height;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLength);
        byte_ptrArray4 destinationData = default;
        int_array4 destinationLineSizes = default;

        fixed (byte* destinationBuffer = buffer)
        {
            int fillResult = ffmpeg.av_image_fill_arrays(
                ref destinationData,
                ref destinationLineSizes,
                destinationBuffer,
                AVPixelFormat.AV_PIX_FMT_RGBA,
                width,
                height,
                1);
            ThrowOnError(fillResult, "Failed to prepare FFmpeg frame output buffer.");

            int scaledRows = ffmpeg.sws_scale(
                _ScaleContext,
                workingFrame->data,
                workingFrame->linesize,
                0,
                height,
                destinationData,
                destinationLineSizes);
            if (scaledRows != height)
            {
                throw new InvalidOperationException($"FFmpeg scaling produced {scaledRows} rows instead of {height}.");
            }
        }

        return new FfmpegDecodedFrame(
            timestamp,
            width,
            height,
            stride,
            FrameByteFormat.RGBA8888,
            buffer,
            bufferLength,
            isHardwareAccelerated: _HardwareDecodeConfig.HasValue,
            returnBufferToPool: true);
    }

    private void EnsureScaleContext(int width, int height, AVPixelFormat sourcePixelFormat)
    {
        if (_ScaleContext != null &&
            _ScaleContextSourceWidth == width &&
            _ScaleContextSourceHeight == height &&
            _ScaleContextSourcePixelFormat == sourcePixelFormat)
        {
            return;
        }

        if (_ScaleContext != null)
        {
            ffmpeg.sws_freeContext(_ScaleContext);
            _ScaleContext = null;
        }

        _ScaleContext = ffmpeg.sws_getContext(
            width,
            height,
            sourcePixelFormat,
            width,
            height,
            AVPixelFormat.AV_PIX_FMT_RGBA,
            _SwsFastBilinear,
            null,
            null,
            null);
        if (_ScaleContext == null)
        {
            throw new InvalidOperationException("Failed to create FFmpeg scaling context.");
        }

        _ScaleContextSourceWidth = width;
        _ScaleContextSourceHeight = height;
        _ScaleContextSourcePixelFormat = sourcePixelFormat;
    }

    private void ResetScaleContextState()
    {
        _ScaleContextSourceWidth = 0;
        _ScaleContextSourceHeight = 0;
        _ScaleContextSourcePixelFormat = AVPixelFormat.AV_PIX_FMT_NONE;
    }

    private void DisposeNativeState()
    {
        if (_Handle.IsAllocated)
        {
            _Handle.Free();
        }

        if (_ScaleContext != null)
        {
            ffmpeg.sws_freeContext(_ScaleContext);
            _ScaleContext = null;
        }

        ResetScaleContextState();

        if (_TransferFrame != null)
        {
            AVFrame* frame = _TransferFrame;
            ffmpeg.av_frame_free(&frame);
            _TransferFrame = null;
        }

        if (_DecodeFrame != null)
        {
            AVFrame* frame = _DecodeFrame;
            ffmpeg.av_frame_free(&frame);
            _DecodeFrame = null;
        }

        if (_Packet != null)
        {
            AVPacket* packet = _Packet;
            ffmpeg.av_packet_free(&packet);
            _Packet = null;
        }

        if (_HardwareDeviceContext != null)
        {
            AVBufferRef* hardwareDeviceContext = _HardwareDeviceContext;
            ffmpeg.av_buffer_unref(&hardwareDeviceContext);
            _HardwareDeviceContext = null;
        }

        if (_CodecContext != null)
        {
            AVCodecContext* context = _CodecContext;
            ffmpeg.avcodec_free_context(&context);
            _CodecContext = null;
        }
    }

    private static AVPixelFormat GetHardwarePixelFormat(AVCodecContext* codecContext, AVPixelFormat* pixelFormats)
    {
        if (codecContext == null || pixelFormats == null || codecContext->opaque == null)
        {
            return AVPixelFormat.AV_PIX_FMT_NONE;
        }

        GCHandle handle = GCHandle.FromIntPtr((nint)codecContext->opaque);
        if (!handle.IsAllocated || handle.Target is not FfmpegDecoderSession session || !session._HardwareDecodeConfig.HasValue)
        {
            return AVPixelFormat.AV_PIX_FMT_NONE;
        }

        AVPixelFormat hardwarePixelFormat = session._HardwareDecodeConfig.Value.PixelFormat;
        for (AVPixelFormat* current = pixelFormats; *current != AVPixelFormat.AV_PIX_FMT_NONE; current++)
        {
            if (*current == hardwarePixelFormat)
            {
                return hardwarePixelFormat;
            }
        }

        return AVPixelFormat.AV_PIX_FMT_NONE;
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
