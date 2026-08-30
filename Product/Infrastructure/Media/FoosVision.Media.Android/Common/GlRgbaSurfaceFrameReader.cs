// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using Android.Graphics;
using Android.Opengl;
using Android.OS;
using Android.Views;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using Java.Nio;

namespace FoosVision.Media.Android.Common;

/// <summary>
/// Reads frames rendered into an Android Surface as top-left-origin RGBA8888 buffers.
/// Based on bigflake's MediaCodec examples (https://bigflake.com/mediacodec).
/// </summary>
internal class GlRgbaSurfaceFrameReader : Java.Lang.Object, SurfaceTexture.IOnFrameAvailableListener
{
    private const long _NanosecondsPerSecond = 1000000000;

    private static readonly Source _Log = new("GlRgbaSurfaceFrameReader");
    private static readonly Source _MetricsLog = new("Media.Android.GlRgbaSurfaceFrameReader.Metrics");

    private readonly int _Width;
    private readonly int _Height;
    private readonly Handler _Handler;
    private readonly IGlRgbaFrameSink _FrameSink;
    private readonly float[] _Transform = new float[16];
    private readonly IntervalMetric? _CallbackInterval;
    private readonly IntervalMetric? _CameraTimestampInterval;
    private readonly DurationMetric? _GlReadPixelsTiming;

    private EGLDisplay? _EglDisplay = EGL14.EglNoDisplay;
    private EGLContext? _EglContext = EGL14.EglNoContext;
    private EGLSurface? _EglSurface = EGL14.EglNoSurface;

    private TextureRender? _Renderer;
    private SurfaceTexture? _SurfaceTexture;
    private Surface? _Surface;
    private ByteBuffer? _PixelBuffer;
    private TaskCompletionSource<long>? _PendingFrame;
    private volatile bool _IgnoreFrames = true;

    private GlRgbaSurfaceFrameReader(
        Handler handler,
        int width,
        int height,
        IGlRgbaFrameSink frameSink,
        RuntimeMetricsOptions runtimeMetricsOptions)
    {
        _Handler = handler;
        _Width = width;
        _Height = height;
        _FrameSink = frameSink;

        if (runtimeMetricsOptions.Enabled)
        {
            TimeSpan reportInterval = runtimeMetricsOptions.GetReportInterval();

            _CallbackInterval = new IntervalMetric(runtimeMetricsOptions.CreateMetricName("OnFrameAvailable.CallbackInterval"), _MetricsLog, reportInterval);
            _CameraTimestampInterval = new IntervalMetric(runtimeMetricsOptions.CreateMetricName("OnFrameAvailable.CameraTimestampInterval"), _MetricsLog, reportInterval);
            _GlReadPixelsTiming = new DurationMetric(runtimeMetricsOptions.CreateMetricName("GlReadPixels"), _MetricsLog, reportInterval);
        }

        EglSetup();
        MakeCurrent();
        SetupSurfaceTexture();
    }

    public Surface Surface => _Surface ?? throw new InvalidOperationException("Surface reader is not initialized.");

    public static Task<GlRgbaSurfaceFrameReader> CreateAsync(
        Handler handler,
        int width,
        int height,
        IGlRgbaFrameSink frameSink,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        var tcs = new TaskCompletionSource<GlRgbaSurfaceFrameReader>(TaskCreationOptions.RunContinuationsAsynchronously);
        RuntimeMetricsOptions options = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();

        handler.Post(() =>
        {
            try
            {
                var reader = new GlRgbaSurfaceFrameReader(handler, width, height, frameSink, options);
                tcs.TrySetResult(reader);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    public void Start()
    {
        _IgnoreFrames = false;
    }

    public Task<long> WaitForNextFrameAsync()
    {
        var pendingFrame = new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);

        _Handler.Post(() =>
        {
            if (_PendingFrame != null)
            {
                pendingFrame.TrySetException(new InvalidOperationException("A frame wait is already pending."));
                return;
            }

            _PendingFrame = pendingFrame;
        });

        return pendingFrame.Task;
    }

    public Task StopAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        _Handler.Post(() =>
        {
            try
            {
                _IgnoreFrames = true;
                Release();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    void SurfaceTexture.IOnFrameAvailableListener.OnFrameAvailable(SurfaceTexture? surfaceTexture)
    {
        if (_Renderer == null ||
            _SurfaceTexture == null ||
            _PixelBuffer == null)
        {
            return;
        }

        long callbackTimestamp = 0;
        IntervalMetric? callbackInterval = _CallbackInterval;

        if (callbackInterval != null)
        {
            callbackTimestamp = Stopwatch.GetTimestamp();
        }

        try
        {
            _SurfaceTexture.UpdateTexImage();

            if (_IgnoreFrames)
            {
                return;
            }

            callbackInterval?.Record(callbackTimestamp, Stopwatch.Frequency);

            long timestampNs = _SurfaceTexture.Timestamp;

            _CameraTimestampInterval?.Record(timestampNs, _NanosecondsPerSecond);

            _Renderer.DrawFrame();

            _PixelBuffer.Rewind();
            ReadPixels(_PixelBuffer);

            _FrameSink.OnFrameAvailable(timestampNs, _PixelBuffer.GetDirectBufferAddress(), _Width * _Height * 4);
            _PendingFrame?.TrySetResult(timestampNs);
            _PendingFrame = null;
        }
        catch (Exception ex)
        {
            _PendingFrame?.TrySetException(ex);
            _PendingFrame = null;
            _Log.Error("OnFrameAvailable failed: {0}", ex);
        }
    }

    private void ReadPixels(ByteBuffer pixelBuffer)
    {
        DurationMetric? timing = _GlReadPixelsTiming;

        if (timing == null)
        {
            GLES20.GlReadPixels(0, 0, _Width, _Height, GLES20.GlRgba, GLES20.GlUnsignedByte, pixelBuffer);
            return;
        }

        long started = Stopwatch.GetTimestamp();
        GLES20.GlReadPixels(0, 0, _Width, _Height, GLES20.GlRgba, GLES20.GlUnsignedByte, pixelBuffer);
        timing.RecordElapsed(started);
    }

    private void EglSetup()
    {
        _EglDisplay = EGL14.EglGetDisplay(EGL14.EglDefaultDisplay);

        if (_EglDisplay == EGL14.EglNoDisplay)
        {
            throw new InvalidOperationException("Unable to get EGL display.");
        }

        int[] version = new int[2];

        if (!EGL14.EglInitialize(_EglDisplay, version, 0, version, 1))
        {
            throw new InvalidOperationException("Unable to initialize EGL.");
        }

        int[] attribList =
        {
            EGL14.EglRedSize, 8,
            EGL14.EglGreenSize, 8,
            EGL14.EglBlueSize, 8,
            EGL14.EglAlphaSize, 8,
            EGL14.EglRenderableType, EGL14.EglOpenglEs2Bit,
            EGL14.EglSurfaceType, EGL14.EglPbufferBit,
            EGL14.EglConformant, EGL14.EglOpenglEs2Bit,
            EGL14.EglNone,
        };

        var configs = new EGLConfig[1];
        var numConfigs = new int[1];

        if (!EGL14.EglChooseConfig(_EglDisplay, attribList, 0, configs, 0, configs.Length, numConfigs, 0))
        {
            throw new InvalidOperationException("Unable to find suitable EGLConfig.");
        }

        int[] contextAttribs =
        {
            EGL14.EglContextClientVersion, 2,
            EGL14.EglNone,
        };

        _EglContext = EGL14.EglCreateContext(_EglDisplay, configs[0], EGL14.EglNoContext, contextAttribs, 0);

        if (_EglContext == null ||
            _EglContext == EGL14.EglNoContext)
        {
            throw new InvalidOperationException("Failed to create EGL context.");
        }

        int[] surfaceAttribs =
        {
            EGL14.EglWidth, _Width,
            EGL14.EglHeight, _Height,
            EGL14.EglNone,
        };

        _EglSurface = EGL14.EglCreatePbufferSurface(_EglDisplay, configs[0], surfaceAttribs, 0);

        if (_EglSurface == null ||
            _EglSurface == EGL14.EglNoSurface)
        {
            throw new InvalidOperationException("Failed to create EGL pbuffer surface.");
        }
    }

    private void MakeCurrent()
    {
        if (!EGL14.EglMakeCurrent(_EglDisplay, _EglSurface, _EglSurface, _EglContext))
        {
            throw new InvalidOperationException("Failed to make EGL context current.");
        }
    }

    private void SetupSurfaceTexture()
    {
        _Renderer = new TextureRender();
        _Renderer.SurfaceCreated();

        _SurfaceTexture = new SurfaceTexture(_Renderer.TextureId);
        _SurfaceTexture.SetDefaultBufferSize(_Width, _Height);
        _SurfaceTexture.SetOnFrameAvailableListener(this, _Handler);

        _Surface = new Surface(_SurfaceTexture);
        _PixelBuffer = ByteBuffer.AllocateDirect(_Width * _Height * 4);
        _PixelBuffer.Order(ByteOrder.LittleEndian!);
    }

    private void Release()
    {
        TryIgnore(() => _Surface?.Release(), nameof(_Surface));
        TryIgnore(() => _SurfaceTexture?.Release(), nameof(_SurfaceTexture));

        _Surface = null;
        _SurfaceTexture = null;
        _Renderer = null;
        _PixelBuffer = null;

        if (_EglDisplay != EGL14.EglNoDisplay)
        {
            TryIgnore(() => EGL14.EglDestroySurface(_EglDisplay, _EglSurface), "EglDestroySurface");
            TryIgnore(() => EGL14.EglDestroyContext(_EglDisplay, _EglContext), "EglDestroyContext");
            TryIgnore(() => EGL14.EglMakeCurrent(_EglDisplay, EGL14.EglNoSurface, EGL14.EglNoSurface, EGL14.EglNoContext), "EglMakeCurrent");
            TryIgnore(() => EGL14.EglTerminate(_EglDisplay), "EglTerminate");
        }

        _EglDisplay = EGL14.EglNoDisplay;
        _EglContext = EGL14.EglNoContext;
        _EglSurface = EGL14.EglNoSurface;
    }

    private static void TryIgnore(Action action, string what)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _Log.Error("Release cleanup for {What} failed with exception {Ex}.", what, ex);
        }
    }
}
