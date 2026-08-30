// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Android.Media;
using Android.OS;
using FoosVision.Common.Logging;
using FoosVision.Media.Android.Common;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Android.Decoding;

public class AndroidVideoDecoder : IAndroidVideoDecoder, IGlRgbaFrameSink
{
    private const long _InputTimeoutUs = 10_000;
    private const long _OutputTimeoutUs = 0;
    private const long _FlushOutputTimeoutUs = 10_000;
    private const int _FlushTryAgainLimit = 100;
    private const int _InputTryAgainLimit = 100;
    private static readonly TimeSpan _FrameReadTimeout = TimeSpan.FromSeconds(2);

    private static readonly Source _Log = new("Media.Android.VideoDecoder");

    private readonly Lock _Lock = new();
    private readonly Lock _FrameLock = new();
    private readonly AccessUnitPreprocessor _AccessUnitPreprocessor;
    private readonly List<AccessUnitDispatch> _Dispatches;
    private readonly Queue<AndroidDecodedFrame> _DecodedFrames;

    private HandlerThread? _GlThread;
    private Handler? _GlHandler;
    private GlRgbaSurfaceFrameReader? _FrameReader;
    private MediaCodec? _Codec;
    private MediaCodec.BufferInfo? _BufferInfo;
    private bool _Disposed;
    private bool _EndOfStreamQueued;

    public AndroidVideoDecoder()
    {
        _AccessUnitPreprocessor = new AccessUnitPreprocessor();
        _Dispatches = [];
        _DecodedFrames = [];
    }

    public bool IsConfigured { get; private set; }

    public AndroidVideoDecoderOptions? Options { get; private set; }

    public void Configure(AndroidVideoDecoderOptions options)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        lock (_Lock)
        {
            ResetInternal();
            DisposeCodecState();

            StartGlThread();
            _FrameReader = GlRgbaSurfaceFrameReader.CreateAsync(_GlHandler!, options.Width, options.Height, this)
                .GetAwaiter()
                .GetResult();
            _FrameReader.Start();

            string mimeType = GetMimeType(options.Codec);
            MediaFormat format = MediaFormat.CreateVideoFormat(mimeType, options.Width, options.Height);

            _Codec = MediaCodec.CreateDecoderByType(mimeType);
            _Codec.Configure(format, _FrameReader.Surface, null, MediaCodecConfigFlags.None);
            _Codec.Start();

            _BufferInfo = new MediaCodec.BufferInfo();
            Options = options;
            IsConfigured = true;

            _Log.Information("Configured Android decoder: {0}", _Codec.Name);
        }
    }

    public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool queueDecodedFrames = true)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_Lock)
        {
            EnsureConfigured();

            if (_EndOfStreamQueued)
            {
                throw new InvalidOperationException("Decoder has been flushed and cannot accept more access units before reset.");
            }

            _Dispatches.Clear();
            if (!_AccessUnitPreprocessor.TryPrepare(buffer, Options!.Codec, timeNs, isKeyFrame, queueDecodedFrames, _Dispatches))
            {
                return;
            }

            foreach (AccessUnitDispatch dispatch in _Dispatches)
            {
                QueueInput(dispatch);
                DrainOutput(dispatch.QueueDecodedFrames, waitForEndOfStream: false);
            }
        }
    }

    public bool TryDequeueFrame([NotNullWhen(true)] out AndroidDecodedFrame? frame)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_FrameLock)
        {
            if (_DecodedFrames.Count == 0)
            {
                frame = null;
                return false;
            }

            frame = _DecodedFrames.Dequeue();
            return true;
        }
    }

    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_Lock)
        {
            EnsureConfigured();

            if (!_EndOfStreamQueued)
            {
                QueueEndOfStream();
                _EndOfStreamQueued = true;
            }

            DrainOutput(queueDecodedFrames: true, waitForEndOfStream: true);
        }
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_Lock)
        {
            ResetInternal();

            if (_Codec != null)
            {
                _Codec.Flush();
            }
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

    void IGlRgbaFrameSink.OnFrameAvailable(long timestampNs, nint bufferAddress, int bufferLength)
    {
        AndroidVideoDecoderOptions? options = Options;
        if (options == null)
        {
            return;
        }

        int stride = options.Width * 4;
        int requiredLength = stride * options.Height;
        if (bufferLength < requiredLength)
        {
            _Log.Warning("Decoded GL frame is too small. Required {0}, available {1}.", requiredLength, bufferLength);
            return;
        }

        byte[] buffer = ArrayPool<byte>.Shared.Rent(requiredLength);
        Marshal.Copy(bufferAddress, buffer, 0, requiredLength);

        lock (_FrameLock)
        {
            _DecodedFrames.Enqueue(new AndroidDecodedFrame(
                timestampNs,
                options.Width,
                options.Height,
                stride,
                FrameByteFormat.RGBA8888,
                buffer,
                requiredLength,
                returnBufferToPool: true));
        }
    }

    private void QueueInput(AccessUnitDispatch dispatch)
    {
        int inputIndex = DequeueInputBuffer(dispatch.QueueDecodedFrames);
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

    private void QueueEndOfStream()
    {
        int inputIndex = DequeueInputBuffer(queueDecodedFrames: true);
        MediaCodec codec = _Codec ?? throw new InvalidOperationException("Decoder codec is not initialized.");

        codec.QueueInputBuffer(inputIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
    }

    private int DequeueInputBuffer(bool queueDecodedFrames)
    {
        for (int i = 0; i < _InputTryAgainLimit; i++)
        {
            int inputIndex = _Codec!.DequeueInputBuffer(_InputTimeoutUs);
            if (inputIndex >= 0)
            {
                return inputIndex;
            }

            DrainOutput(queueDecodedFrames, waitForEndOfStream: false);
        }

        throw new InvalidOperationException("Timed out waiting for a decoder input buffer.");
    }

    private void DrainOutput(bool queueDecodedFrames, bool waitForEndOfStream)
    {
        MediaCodec.BufferInfo bufferInfo = _BufferInfo ?? throw new InvalidOperationException("Decoder buffer info is not initialized.");
        int tryAgainCount = 0;

        while (true)
        {
            long timeoutUs = waitForEndOfStream ? _FlushOutputTimeoutUs : _OutputTimeoutUs;
            int outputIndex = _Codec!.DequeueOutputBuffer(bufferInfo, timeoutUs);

            if (outputIndex == (int)MediaCodecInfoState.TryAgainLater)
            {
                if (waitForEndOfStream && tryAgainCount < _FlushTryAgainLimit)
                {
                    tryAgainCount++;
                    continue;
                }

                return;
            }

            tryAgainCount = 0;

            if (outputIndex == (int)MediaCodecInfoState.OutputFormatChanged ||
                outputIndex == (int)MediaCodecInfoState.OutputBuffersChanged)
            {
                continue;
            }

            if (outputIndex < 0)
            {
                return;
            }

            bool render = queueDecodedFrames && bufferInfo.Size > 0;
            Task<long>? frameReadTask = render
                ? _FrameReader?.WaitForNextFrameAsync()
                : null;

            _Codec.ReleaseOutputBuffer(outputIndex, render);

            if (frameReadTask != null)
            {
                WaitForFrameRead(frameReadTask);
            }

            if ((bufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) == MediaCodecBufferFlags.EndOfStream)
            {
                return;
            }
        }
    }

    private static void WaitForFrameRead(Task<long> frameReadTask)
    {
        if (!frameReadTask.Wait(_FrameReadTimeout))
        {
            throw new TimeoutException("Timed out waiting for rendered decoder frame to be read as RGBA.");
        }

        _ = frameReadTask.GetAwaiter().GetResult();
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
        _EndOfStreamQueued = false;

        lock (_FrameLock)
        {
            while (_DecodedFrames.Count != 0)
            {
                _DecodedFrames.Dequeue().Dispose();
            }
        }
    }

    private void DisposeCodecState()
    {
        TryIgnore(() => _Codec?.Stop(), "Codec.Stop");
        TryIgnore(() => _Codec?.Release(), "Codec.Release");
        _Codec?.Dispose();
        _Codec = null;
        _BufferInfo = null;

        if (_FrameReader != null)
        {
            try
            {
                _FrameReader.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _Log.Warning("Frame reader stop failed: {0}", ex);
            }

            _FrameReader = null;
        }

        StopGlThread();
        IsConfigured = false;
        Options = null;
    }

    private void StartGlThread()
    {
        _GlThread ??= new HandlerThread("FoosVision.Decoder.GL");
        if (!_GlThread.IsAlive)
        {
            _GlThread.Start();
        }

        _GlHandler ??= new Handler(_GlThread.Looper!);
    }

    private void StopGlThread()
    {
        HandlerThread? thread = _GlThread;
        if (thread != null)
        {
            try
            {
                thread.QuitSafely();
            }
            catch
            {
            }

            try
            {
                if (Java.Lang.Thread.CurrentThread() != thread)
                {
                    thread.Join();
                }
            }
            catch
            {
            }
        }

        _GlHandler = null;
        _GlThread?.Dispose();
        _GlThread = null;
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
}
