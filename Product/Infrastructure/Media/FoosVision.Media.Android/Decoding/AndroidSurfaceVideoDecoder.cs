// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Media;
using Android.OS;
using Android.Views;
using FoosVision.Common.Logging;
using FoosVision.Media.Android.Common;
using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Android.Decoding;

public class FrameRenderedEventArgs : EventArgs
{
    public FrameRenderedEventArgs(long timeNs)
    {
        TimeNs = timeNs;
    }

    public long TimeNs { get; }
}

public class AndroidSurfaceVideoDecoder : IDisposable
{
    private const long _InputTimeoutUs = 10_000;
    private const long _OutputTimeoutUs = 0;
    private const int _InputTryAgainLimit = 100;
    private const string _MediaFormatKeyLowLatency = "low-latency";
    private static readonly Source _Log = new("Media.Android.SurfaceVideoDecoder");

    private readonly Lock _Lock = new();
    private readonly AccessUnitPreprocessor _AccessUnitPreprocessor = new();
    private readonly List<AccessUnitDispatch> _Dispatches = [];

    private MediaCodec? _Codec;
    private MediaCodec.BufferInfo? _BufferInfo;
    private bool _Disposed;

    public bool IsConfigured { get; private set; }

    public AndroidVideoDecoderOptions? Options { get; private set; }

    public event EventHandler<FrameRenderedEventArgs>? FrameRendered;

    public void Configure(AndroidVideoDecoderOptions options, Surface surface)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(surface);

        options.Validate();

        lock (_Lock)
        {
            ResetInternal();
            DisposeCodecState();

            string mimeType = GetMimeType(options.Codec);
            MediaFormat format = MediaFormat.CreateVideoFormat(mimeType, options.Width, options.Height);

            if (options.EnableLowLatency)
            {
                format.SetInteger(_MediaFormatKeyLowLatency, 1);
            }

            _Codec = MediaCodec.CreateDecoderByType(mimeType);
            _Codec.Configure(format, surface, null, MediaCodecConfigFlags.None);
            _Codec.SetOnFrameRenderedListener(new FrameRenderedListener(this), new Handler(Looper.MainLooper!));
            _Codec.Start();

            _BufferInfo = new MediaCodec.BufferInfo();
            Options = options;
            IsConfigured = true;

            _Log.Information("Configured Android surface decoder: {0}", _Codec.Name);
        }
    }

    public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_Lock)
        {
            EnsureConfigured();

            _Dispatches.Clear();
            if (!_AccessUnitPreprocessor.TryPrepare(buffer, Options!.Codec, timeNs, isKeyFrame, queueDecodedFrames: true, _Dispatches))
            {
                return;
            }

            foreach (AccessUnitDispatch dispatch in _Dispatches)
            {
                QueueInput(dispatch);
                DrainOutput();
            }
        }
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_Lock)
        {
            ResetInternal();
            _Codec?.Flush();
        }
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        lock (_Lock)
        {
            _Disposed = true;
            ResetInternal();
            DisposeCodecState();
        }

        GC.SuppressFinalize(this);
    }

    private void QueueInput(AccessUnitDispatch dispatch)
    {
        int inputIndex = DequeueInputBuffer();
        MediaCodec codec = _Codec ?? throw new InvalidOperationException("Decoder codec is not initialized.");

        Java.Nio.ByteBuffer? inputBuffer = codec.GetInputBuffer(inputIndex);
        if (inputBuffer == null)
        {
            throw new InvalidOperationException("Decoder input buffer is not available.");
        }

        inputBuffer.Clear();
        ReadOnlySpan<byte> source = dispatch.Buffer.Span;
        if (source.Length > inputBuffer.Capacity())
        {
            throw new InvalidOperationException("Access unit does not fit into the decoder input buffer.");
        }

        ByteBufferCopy.Put(inputBuffer, source);

        long presentationTimeUs = dispatch.TimeNs / 1000L;
        codec.QueueInputBuffer(inputIndex, 0, source.Length, presentationTimeUs, MediaCodecBufferFlags.None);
    }

    private int DequeueInputBuffer()
    {
        for (int i = 0; i < _InputTryAgainLimit; i++)
        {
            int inputIndex = _Codec!.DequeueInputBuffer(_InputTimeoutUs);
            if (inputIndex >= 0)
            {
                return inputIndex;
            }

            DrainOutput();
        }

        throw new InvalidOperationException("Timed out waiting for a decoder input buffer.");
    }

    private void DrainOutput()
    {
        MediaCodec.BufferInfo bufferInfo = _BufferInfo ?? throw new InvalidOperationException("Decoder buffer info is not initialized.");

        while (true)
        {
            int outputIndex = _Codec!.DequeueOutputBuffer(bufferInfo, _OutputTimeoutUs);

            if (outputIndex == (int)MediaCodecInfoState.TryAgainLater)
            {
                return;
            }

            if (outputIndex == (int)MediaCodecInfoState.OutputFormatChanged)
            {
                _Log.Information("Surface decoder output format changed: {0}", _Codec.OutputFormat);
                continue;
            }

            if (outputIndex == (int)MediaCodecInfoState.OutputBuffersChanged)
            {
                continue;
            }

            if (outputIndex < 0)
            {
                return;
            }

            bool render = bufferInfo.Size > 0;
            _Codec.ReleaseOutputBuffer(outputIndex, render);
        }
    }

    private void OnFrameRendered(long presentationTimeUs)
    {
        if (_Disposed)
        {
            return;
        }

        FrameRendered?.Invoke(this, new FrameRenderedEventArgs(presentationTimeUs * 1000L));
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured || Options == null || _Codec == null || _BufferInfo == null)
        {
            throw new InvalidOperationException("Decoder must be configured before pushing access units.");
        }
    }

    private void ResetInternal()
    {
        _AccessUnitPreprocessor.Reset();
        _Dispatches.Clear();
    }

    private void DisposeCodecState()
    {
        TryIgnore(() => _Codec?.Stop(), "Codec.Stop");
        TryIgnore(() => _Codec?.Release(), "Codec.Release");
        _Codec?.Dispose();
        _Codec = null;
        _BufferInfo = null;
        IsConfigured = false;
        Options = null;
    }

    private static string GetMimeType(CodecType codec)
        => codec switch
        {
            CodecType.H264 => MediaFormat.MimetypeVideoAvc,
            CodecType.H265 => MediaFormat.MimetypeVideoHevc,
            _ => throw new NotSupportedException($"Codec '{codec}' is not supported."),
        };

    private static void TryIgnore(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _Log.Warning("Cleanup for {What} failed: {Ex}", what, ex);
        }
    }

    private class FrameRenderedListener : Java.Lang.Object, MediaCodec.IOnFrameRenderedListener
    {
        private readonly AndroidSurfaceVideoDecoder _Owner;

        public FrameRenderedListener(AndroidSurfaceVideoDecoder owner)
        {
            _Owner = owner;
        }

        public void OnFrameRendered(MediaCodec codec, long presentationTimeUs, long nanoTime)
        {
            _Owner.OnFrameRendered(presentationTimeUs);
        }
    }
}
