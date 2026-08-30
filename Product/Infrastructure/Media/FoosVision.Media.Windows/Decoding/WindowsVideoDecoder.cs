// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding.Ffmpeg;

namespace FoosVision.Media.Windows.Decoding;

public class WindowsVideoDecoder : IWindowsVideoDecoder
{
    private readonly IFfmpegDecoderSessionFactory _SessionFactory;
    private readonly Queue<WindowsDecodedFrame> _DecodedFrames;
    private readonly AccessUnitPreprocessor _AccessUnitPreprocessor;
    private readonly List<AccessUnitDispatch> _Dispatches;

    private IFfmpegDecoderSession? _Session;
    private bool _Disposed;

    public WindowsVideoDecoder()
        : this(new FfmpegDecoderSessionFactory())
    {
    }

    internal WindowsVideoDecoder(IFfmpegDecoderSessionFactory sessionFactory)
    {
        _SessionFactory = sessionFactory;
        _DecodedFrames = [];
        _AccessUnitPreprocessor = new AccessUnitPreprocessor();
        _Dispatches = [];
    }

    public bool IsConfigured { get; private set; }

    public bool IsHardwareAccelerated => _Session?.IsHardwareAccelerated ?? false;

    public WindowsVideoDecoderOptions? Options { get; private set; }

    public void Configure(WindowsVideoDecoderOptions options)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();

        ResetInternal();
        DisposeSession();

        FfmpegDecoderOptions ffmpegOptions = new(
            options.Codec,
            options.Width,
            options.Height,
            options.OutputFormat,
            options.HardwareMode);

        try
        {
            _Session = _SessionFactory.Create(ffmpegOptions);
            _Session.Configure(ffmpegOptions);
        }
        catch when (options.HardwareMode == WindowsVideoDecoderHardwareMode.PreferHardware)
        {
            _Session?.Dispose();
            _Session = null;

            FfmpegDecoderOptions softwareOptions = ffmpegOptions with
            {
                HardwareMode = WindowsVideoDecoderHardwareMode.SoftwareOnly,
            };

            _Session = _SessionFactory.Create(softwareOptions);
            _Session.Configure(softwareOptions);
        }

        Options = options;
        IsConfigured = true;
    }

    public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool queueDecodedFrames = true)
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        if (!IsConfigured || Options == null || _Session == null)
        {
            throw new InvalidOperationException("Decoder must be configured before pushing access units.");
        }

        if (buffer.IsEmpty)
        {
            throw new ArgumentException("Access unit must not be empty.", nameof(buffer));
        }

        _Dispatches.Clear();
        if (!_AccessUnitPreprocessor.TryPrepare(buffer, Options.Codec, timeNs, isKeyFrame, queueDecodedFrames, _Dispatches))
        {
            return;
        }

        foreach (AccessUnitDispatch dispatch in _Dispatches)
        {
            _Session.PushAccessUnit(dispatch.Buffer.Span, dispatch.TimeNs, dispatch.IsKeyFrame, dispatch.QueueDecodedFrames);
            if (dispatch.QueueDecodedFrames)
            {
                DrainSessionFrames();
            }
        }
    }

    public bool TryDequeueFrame([NotNullWhen(true)] out WindowsDecodedFrame? frame)
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

        if (!IsConfigured || _Session == null)
        {
            return;
        }

        _Session.Flush();
        DrainSessionFrames();
    }

    public void Reset()
    {
        ObjectDisposedException.ThrowIf(_Disposed, this);

        ResetInternal();

        if (_Session != null)
        {
            _Session.Reset();
        }
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        ResetInternal();
        DisposeSession();
        GC.SuppressFinalize(this);
    }

    private void DrainSessionFrames()
    {
        if (_Session == null)
        {
            return;
        }

        while (_Session.TryDequeueFrame(out FfmpegDecodedFrame? frame))
        {
            using (frame)
            {
                WindowsDecodedFrame windowsFrame = new(
                    frame.TimeNs,
                    frame.Width,
                    frame.Height,
                    frame.Stride,
                    frame.Format,
                    frame.DetachBuffer(),
                    frame.BufferLength,
                    returnBufferToPool: true);
                _DecodedFrames.Enqueue(windowsFrame);
            }
        }
    }

    private void ResetInternal()
    {
        _AccessUnitPreprocessor.Reset();
        _Dispatches.Clear();

        while (_DecodedFrames.Count != 0)
        {
            _DecodedFrames.Dequeue().Dispose();
        }
    }

    private void DisposeSession()
    {
        _Session?.Dispose();
        _Session = null;
        IsConfigured = false;
        Options = null;
    }
}
