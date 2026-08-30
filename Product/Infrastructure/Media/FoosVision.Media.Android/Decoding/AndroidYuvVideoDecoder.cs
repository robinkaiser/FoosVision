// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Android.Media;
using Android.OS;
using FoosVision.Common.Logging;
using FoosVision.Media.Android.Common;
using FoosVision.Media.Core.DecodedYuvFrames;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Ports.Media;
using Java.Nio;

namespace FoosVision.Media.Android.Decoding;

public class AndroidYuvVideoDecoder : IAndroidYuvVideoDecoder
{
    private const int _ColorFormatYuv420Flexible = 0x7F420888;
    private const int _FlushTryAgainLimit = 100;
    private const int _CallbackWaitTimeoutMs = 10;

    private static readonly Source _Log = new("Media.Android.YuvVideoDecoder");

    private readonly Lock _Lock = new();
    private readonly object _CallbackGate = new();
    private readonly AccessUnitPreprocessor _AccessUnitPreprocessor;
    private readonly List<AccessUnitDispatch> _Dispatches;
    private readonly Queue<AccessUnitDispatch> _PendingInputs;
    private readonly Queue<int> _AvailableInputBuffers;
    private readonly Queue<PendingOutputBuffer> _AvailableOutputBuffers;
    private readonly Queue<AndroidYuvDecodedFrame> _DecodedFrames;
    private readonly int _PoolSize;

    private HandlerThread? _CallbackThread;
    private Handler? _CallbackHandler;
    private DecoderCallback? _Callback;
    private YuvFramePool? _FramePool;
    private MediaCodec? _Codec;
    private Exception? _CallbackException;
    private PendingOutputBuffer? _BlockedOutputBuffer;
    private bool _Disposed;
    private bool _FlushRequested;
    private bool _EndOfStreamQueued;
    private bool _OutputEndOfStreamReceived;

    public AndroidYuvVideoDecoder()
        : this(poolSize: 8)
    {
    }

    public AndroidYuvVideoDecoder(int poolSize)
    {
        _PoolSize = poolSize;
        _AccessUnitPreprocessor = new AccessUnitPreprocessor();
        _Dispatches = [];
        _PendingInputs = [];
        _AvailableInputBuffers = [];
        _AvailableOutputBuffers = [];
        _DecodedFrames = [];
    }

    public bool IsConfigured { get; private set; }

    public AndroidVideoDecoderOptions? Options { get; private set; }

    public bool IsEndOfStreamDrained
    {
        get
        {
            lock (_Lock)
            {
                return _OutputEndOfStreamReceived &&
                    _DecodedFrames.Count == 0 &&
                    _BlockedOutputBuffer == null;
            }
        }
    }

    public void Configure(AndroidVideoDecoderOptions options)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        lock (_Lock)
        {
            ResetInternal();
            DisposeCodecState();

            string mimeType = GetMimeType(options.Codec);
            MediaFormat format = MediaFormat.CreateVideoFormat(mimeType, options.Width, options.Height);
            format.SetInteger(MediaFormat.KeyColorFormat, _ColorFormatYuv420Flexible);

            StartCallbackThread();
            _Codec = MediaCodec.CreateDecoderByType(mimeType);
            _Callback = new DecoderCallback(this);
            _Codec.SetCallback(_Callback, _CallbackHandler!);
            _Codec.Configure(format, null, null, MediaCodecConfigFlags.None);
            _Codec.Start();

            Options = options;
            IsConfigured = true;

            _Log.Information("Configured Android YUV decoder: {0}", _Codec.Name);
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
                _PendingInputs.Enqueue(dispatch);
            }

            Pump(queueDecodedFrames: true, waitForPendingInput: false, waitForEndOfStream: false);
        }
    }

    public bool TryDequeueFrame([NotNullWhen(true)] out AndroidYuvDecodedFrame? frame)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_Lock)
        {
            Pump(queueDecodedFrames: true, waitForPendingInput: false, waitForEndOfStream: false);

            if (_FlushRequested && !_EndOfStreamQueued)
            {
                TryQueueEndOfStream();
            }

            _ = DrainOutput(queueDecodedFrames: true, waitForEndOfStream: false);

            if (_DecodedFrames.Count == 0)
            {
                frame = null;
                return false;
            }

            frame = _DecodedFrames.Dequeue();
            return true;
        }
    }

    public void Flush(bool waitForEndOfStream = false)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        lock (_Lock)
        {
            EnsureConfigured();

            if (!_EndOfStreamQueued)
            {
                _FlushRequested = true;
                Pump(queueDecodedFrames: true, waitForPendingInput: waitForEndOfStream, waitForEndOfStream: false);
                TryQueueEndOfStream();
            }

            _ = DrainOutput(queueDecodedFrames: true, waitForEndOfStream);
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
                ClearCodecQueues();
                _Codec.Start();
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

    private void Pump(bool queueDecodedFrames, bool waitForPendingInput, bool waitForEndOfStream)
    {
        MediaCodec codec = _Codec ?? throw new InvalidOperationException("Decoder codec is not initialized.");
        int idleCount = 0;

        while (true)
        {
            bool madeProgress = DrainOutput(queueDecodedFrames, waitForEndOfStream: false);

            if (_BlockedOutputBuffer != null && !waitForPendingInput)
            {
                return;
            }

            while (_PendingInputs.TryPeek(out AccessUnitDispatch dispatch) &&
                   TryDequeueInputBuffer(out int inputIndex))
            {
                _PendingInputs.Dequeue();
                QueueInput(codec, inputIndex, dispatch);
                madeProgress = true;
            }

            if (_PendingInputs.Count == 0)
            {
                if (waitForEndOfStream)
                {
                    _ = DrainOutput(queueDecodedFrames, waitForEndOfStream: true);
                }

                return;
            }

            if (!waitForPendingInput && !madeProgress)
            {
                return;
            }

            if (!madeProgress)
            {
                if (_BlockedOutputBuffer != null && !waitForPendingInput)
                {
                    return;
                }

                idleCount++;
                if (idleCount > _FlushTryAgainLimit)
                {
                    throw new InvalidOperationException("Timed out waiting for decoder input capacity after draining output.");
                }

                WaitForCodecCallback();
            }
            else
            {
                idleCount = 0;
            }
        }
    }

    private void QueueInput(MediaCodec codec, int inputIndex, AccessUnitDispatch dispatch)
    {
        ByteBuffer? inputBuffer = codec.GetInputBuffer(inputIndex);
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

    private bool TryQueueEndOfStream()
    {
        if (!_FlushRequested || _EndOfStreamQueued || _PendingInputs.Count != 0 || _BlockedOutputBuffer != null)
        {
            return false;
        }

        MediaCodec codec = _Codec ?? throw new InvalidOperationException("Decoder codec is not initialized.");

        for (int i = 0; i < _FlushTryAgainLimit; i++)
        {
            _ = DrainOutput(queueDecodedFrames: true, waitForEndOfStream: false);
            if (TryDequeueInputBuffer(out int inputIndex))
            {
                codec.QueueInputBuffer(inputIndex, 0, 0, 0, MediaCodecBufferFlags.EndOfStream);
                _EndOfStreamQueued = true;
                return true;
            }

            WaitForCodecCallback();
        }

        throw new InvalidOperationException("Timed out waiting for a decoder input buffer.");
    }

    private bool DrainOutput(bool queueDecodedFrames, bool waitForEndOfStream)
    {
        int tryAgainCount = 0;
        bool drainedAny = false;

        while (true)
        {
            if (!TryDequeueOutputBuffer(out PendingOutputBuffer outputBuffer))
            {
                if (waitForEndOfStream && !_OutputEndOfStreamReceived && tryAgainCount < _FlushTryAgainLimit)
                {
                    tryAgainCount++;
                    WaitForCodecCallback();
                    continue;
                }

                return drainedAny;
            }

            tryAgainCount = 0;

            try
            {
                if (queueDecodedFrames && outputBuffer.BufferInfo.Size > 0)
                {
                    if (!TryReadYuvFrame(outputBuffer.Index, outputBuffer.BufferInfo))
                    {
                        _BlockedOutputBuffer = outputBuffer;
                        return drainedAny;
                    }
                }
            }
            finally
            {
                if (_BlockedOutputBuffer != outputBuffer)
                {
                    _Codec!.ReleaseOutputBuffer(outputBuffer.Index, render: false);
                }
            }

            drainedAny = true;

            if ((outputBuffer.BufferInfo.Flags & MediaCodecBufferFlags.EndOfStream) == MediaCodecBufferFlags.EndOfStream)
            {
                _OutputEndOfStreamReceived = true;
                return true;
            }
        }
    }

    private bool TryReadYuvFrame(int outputIndex, MediaCodec.BufferInfo bufferInfo)
    {
        global::Android.Media.Image? image = _Codec!.GetOutputImage(outputIndex);
        if (image == null)
        {
            throw new NotSupportedException("MediaCodec did not expose decoded output as an Image.");
        }

        try
        {
            global::Android.Media.Image.Plane[]? planes = image.GetPlanes();
            if (planes == null)
            {
                throw new NotSupportedException("MediaCodec output image does not expose YUV planes.");
            }

            if (planes.Length < 3)
            {
                throw new NotSupportedException($"Expected at least 3 YUV planes, got {planes.Length}.");
            }

            EnsureFramePool(image, planes);
            IProducerYuvFrameHandle handle = _FramePool!.AcquireForWrite();

            if (handle is NullProducerYuvFrameHandle)
            {
                _Log.Warning("YUV decoder paused output draining because the frame pool is exhausted.");
                return false;
            }

            CopyPlane(planes[0], handle.BufferY);
            CopyPlane(planes[1], handle.BufferU);
            CopyPlane(planes[2], handle.BufferV);
            long timeNs = bufferInfo.PresentationTimeUs * 1000L;
            handle.MarkWritten(timeNs);
            return true;
        }
        finally
        {
            image.Close();
            image.Dispose();
        }
    }

    private void EnsureFramePool(global::Android.Media.Image image, global::Android.Media.Image.Plane[] planes)
    {
        YuvFrameLayout layout = CreateLayout(image, planes);
        if (_FramePool != null && _FramePool.Layout == layout)
        {
            return;
        }

        _FramePool = new YuvFramePool(_PoolSize, layout);
        _FramePool.SetFrameReadyNotificationSink(lease => _DecodedFrames.Enqueue(new AndroidYuvDecodedFrame(lease)));
    }

    private static YuvFrameLayout CreateLayout(global::Android.Media.Image image, global::Android.Media.Image.Plane[] planes)
    {
        YuvPlaneLayout y = new(YuvPlaneKind.Y, image.Width, image.Height, planes[0].RowStride, planes[0].PixelStride);
        YuvPlaneLayout u = new(YuvPlaneKind.U, (image.Width + 1) / 2, (image.Height + 1) / 2, planes[1].RowStride, planes[1].PixelStride);
        YuvPlaneLayout v = new(YuvPlaneKind.V, (image.Width + 1) / 2, (image.Height + 1) / 2, planes[2].RowStride, planes[2].PixelStride);
        return new YuvFrameLayout(image.Width, image.Height, y, u, v);
    }

    private static void CopyPlane(global::Android.Media.Image.Plane plane, byte[] destination)
    {
        ByteBuffer? planeBuffer = plane.Buffer;
        if (planeBuffer == null)
        {
            throw new NotSupportedException("MediaCodec output image plane does not expose a buffer.");
        }

        ByteBuffer buffer = planeBuffer;
        int oldPosition = buffer.Position();

        try
        {
            buffer.Position(0);
            int length = buffer.Remaining();
            if (length > destination.Length)
            {
                throw new InvalidOperationException("YUV plane does not fit into the pooled destination buffer.");
            }

            nint sourceAddress = buffer.GetDirectBufferAddress();
            if (sourceAddress != 0)
            {
                Marshal.Copy(sourceAddress, destination, 0, length);
                return;
            }

            buffer.Get(destination, 0, length);
        }
        finally
        {
            buffer.Position(oldPosition);
        }
    }

    private bool TryDequeueInputBuffer(out int inputIndex)
    {
        lock (_CallbackGate)
        {
            ThrowIfCallbackFailed();

            if (_AvailableInputBuffers.Count == 0)
            {
                inputIndex = -1;
                return false;
            }

            inputIndex = _AvailableInputBuffers.Dequeue();
            return true;
        }
    }

    private bool TryDequeueOutputBuffer(out PendingOutputBuffer outputBuffer)
    {
        if (_BlockedOutputBuffer is PendingOutputBuffer blockedOutputBuffer)
        {
            _BlockedOutputBuffer = null;
            outputBuffer = blockedOutputBuffer;
            return true;
        }

        lock (_CallbackGate)
        {
            ThrowIfCallbackFailed();

            if (_AvailableOutputBuffers.Count == 0)
            {
                outputBuffer = default;
                return false;
            }

            outputBuffer = _AvailableOutputBuffers.Dequeue();
            return true;
        }
    }

    private void WaitForCodecCallback()
    {
        lock (_CallbackGate)
        {
            ThrowIfCallbackFailed();
            Monitor.Wait(_CallbackGate, _CallbackWaitTimeoutMs);
            ThrowIfCallbackFailed();
        }
    }

    private void OnInputBufferAvailable(int index)
    {
        lock (_CallbackGate)
        {
            if (_Disposed)
            {
                return;
            }

            _AvailableInputBuffers.Enqueue(index);
            Monitor.PulseAll(_CallbackGate);
        }
    }

    private void OnOutputBufferAvailable(int index, MediaCodec.BufferInfo info)
    {
        var bufferInfo = new MediaCodec.BufferInfo();
        bufferInfo.Set(info.Offset, info.Size, info.PresentationTimeUs, info.Flags);

        lock (_CallbackGate)
        {
            if (_Disposed)
            {
                return;
            }

            _AvailableOutputBuffers.Enqueue(new PendingOutputBuffer(index, bufferInfo));
            Monitor.PulseAll(_CallbackGate);
        }
    }

    private void OnCodecError(MediaCodec.CodecException exception)
    {
        lock (_CallbackGate)
        {
            _CallbackException = exception;
            Monitor.PulseAll(_CallbackGate);
        }
    }

    private void ClearCodecQueues()
    {
        lock (_CallbackGate)
        {
            _AvailableInputBuffers.Clear();
            _AvailableOutputBuffers.Clear();
            _BlockedOutputBuffer = null;
            _CallbackException = null;
            _OutputEndOfStreamReceived = false;
        }
    }

    private void ThrowIfCallbackFailed()
    {
        if (_CallbackException != null)
        {
            throw new InvalidOperationException("MediaCodec callback failed.", _CallbackException);
        }
    }

    private void EnsureConfigured()
    {
        if (!IsConfigured || Options == null || _Codec == null)
        {
            throw new InvalidOperationException("Decoder must be configured before pushing access units.");
        }
    }

    private void ResetInternal()
    {
        _AccessUnitPreprocessor.Reset();
        _Dispatches.Clear();
        _PendingInputs.Clear();
        _FlushRequested = false;
        _EndOfStreamQueued = false;
        ClearCodecQueues();

        while (_DecodedFrames.Count != 0)
        {
            _DecodedFrames.Dequeue().Release();
        }

        _FramePool = null;
    }

    private void DisposeCodecState()
    {
        TryIgnore(() => _Codec?.Stop(), "Codec.Stop");
        TryIgnore(() => _Codec?.Release(), "Codec.Release");
        _Codec?.Dispose();
        _Codec = null;
        _Callback = null;

        StopCallbackThread();

        IsConfigured = false;
        Options = null;
    }

    private void StartCallbackThread()
    {
        _CallbackThread ??= new HandlerThread("FoosVision.Decoder.YUV");
        if (!_CallbackThread.IsAlive)
        {
            _CallbackThread.Start();
        }

        _CallbackHandler ??= new Handler(_CallbackThread.Looper!);
    }

    private void StopCallbackThread()
    {
        HandlerThread? thread = _CallbackThread;
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

        _CallbackHandler = null;
        _CallbackThread?.Dispose();
        _CallbackThread = null;
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

    private readonly record struct PendingOutputBuffer(int Index, MediaCodec.BufferInfo BufferInfo);

    private class DecoderCallback : MediaCodec.Callback
    {
        private readonly AndroidYuvVideoDecoder _Owner;

        public DecoderCallback(AndroidYuvVideoDecoder owner)
        {
            _Owner = owner;
        }

        public override void OnInputBufferAvailable(MediaCodec codec, int index)
            => _Owner.OnInputBufferAvailable(index);

        public override void OnOutputBufferAvailable(MediaCodec codec, int index, MediaCodec.BufferInfo info)
            => _Owner.OnOutputBufferAvailable(index, info);

        public override void OnError(MediaCodec codec, MediaCodec.CodecException e)
            => _Owner.OnCodecError(e);

        public override void OnOutputFormatChanged(MediaCodec codec, MediaFormat format)
            => _Log.Information("YUV decoder output format changed: {0}", format);
    }
}
