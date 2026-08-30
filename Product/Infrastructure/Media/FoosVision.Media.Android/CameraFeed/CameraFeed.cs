// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Content;
using Android.Hardware.Camera2;
using Android.OS;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Media.Android.Common;
using FoosVision.Media.Core.Capture;
using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Android.CameraFeed;

/// <summary>
/// Android Camera2 high-speed implementation. Produces:
/// -  30 fps RGBA frames from preview stream via IFrameSink.
/// - 120 fps H.264 encoded via IEncodedAccessUnitSink.
/// </summary>
public class CameraFeed : ICameraFeed
{
    private static readonly Source _Log = new("Media.Android.CameraFeed");

    private readonly Context _AppContext;
    private readonly RuntimeMetricsOptions _RuntimeMetricsOptions;
    private readonly SemaphoreSlim _Gate = new(1, 1);

    private AndroidHighSpeedProfile? _Profile;
    private HandlerThread? _CameraThread;
    private Handler? _CameraHandler;
    private HandlerThread? _CodecThread;
    private Handler? _CodecHandler;
    private HandlerThread? _GlThread;
    private Handler? _GlHandler;

    private CameraDevice? _Device;
    private CameraCaptureSession? _Session;

    private GlRgbaPreviewSurface? _Preview;
    private Encoder? _Encoder;

    private volatile bool _IsRunning;

    public CameraFeed(Context context, RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _AppContext = context.ApplicationContext!;
        _RuntimeMetricsOptions = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();
    }

    public async Task<bool> Configure()
    {
        await _Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_IsRunning)
            {
                _Log.Warning("Configure called while running.");
                return false;
            }

            var cm = (CameraManager)_AppContext.GetSystemService(Context.CameraService)!;

            _Profile = AndroidCameraSelector.SelectDefaultHighSpeedProfile(cm);

            if (_Profile == null)
            {
                _Log.Error("Configure failed: no suitable high-speed camera/profile found.");
                return false;
            }

            _Log.Information(
                "Selected camera {0} {1}x{2} @{3}fps (preview sampled @{4}fps).",
                _Profile.CameraId,
                _Profile.Width,
                _Profile.Height,
                _Profile.SlowMoFps,
                _Profile.PreviewFps);

            return true;
        }
        catch (Exception ex)
        {
            _Log.Error("Configure failed: {0}", ex);
            return false;
        }
        finally
        {
            _Gate.Release();
        }
    }

    public async Task<bool> Start(IFrameSink frameSink, IEncodedAccessUnitSink encodedUnitSink)
    {
        await _Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_IsRunning)
            {
                _Log.Warning("Start called while already running.");
                return false;
            }

            if (_Profile == null)
            {
                _Log.Warning("Start called before Configure().");
                return false;
            }

            StartThreads();

            // Create GL preview surface on GL thread (EGL context must live on the creating thread).
            _Preview = await GlRgbaPreviewSurface.CreateAsync(
                handler: _GlHandler!,
                width: _Profile.Width,
                height: _Profile.Height,
                frameSink: frameSink,
                runtimeMetricsOptions: _RuntimeMetricsOptions).ConfigureAwait(false);

            // Create encoder on codec thread.
            _Encoder = await Encoder.CreateAsync(
                handler: _CodecHandler!,
                MediaFormatFactory.MimeTypeAvc,
                MediaFormatFactory.CreateAvc(_Profile.Width, _Profile.Height, _Profile.SlowMoFps),
                encodedSink: encodedUnitSink,
                runtimeMetricsOptions: _RuntimeMetricsOptions).ConfigureAwait(false);

            await _Encoder.StartAsync().ConfigureAwait(false);

            // Open camera and create high-speed session.
            _Device = await AndroidCamera2.OpenAsync(
                _AppContext,
                _CameraHandler!,
                _Profile.CameraId).ConfigureAwait(false);

            var previewSurface = _Preview.Surface;
            var recordSurface = _Encoder.InputSurface;

            _Session = await AndroidCamera2.CreateHighSpeedSessionAsync(
                _CameraHandler!,
                _Device,
                previewSurface,
                recordSurface).ConfigureAwait(false);

            AndroidCamera2.StartHighSpeedRepeating(
                _Session,
                _Device,
                previewSurface,
                recordSurface,
                _Profile.SlowMoFps);

            // Start delivery
            _Preview.Start();

            _IsRunning = true;
            _Log.Information("CameraFeed started.");
            return true;
        }
        catch (Exception ex)
        {
            _Log.Error("Start failed: {0}", ex);
            await StopInternal().ConfigureAwait(false);
            return false;
        }
        finally
        {
            _Gate.Release();
        }
    }

    public async Task Stop()
    {
        await _Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopInternal().ConfigureAwait(false);
        }
        finally
        {
            _Gate.Release();
        }
    }

    private async Task StopInternal()
    {
        if (!_IsRunning && _Device == null && _Preview == null && _Encoder == null)
        {
            StopThreads();
            return;
        }

        _IsRunning = false;

        try
        {
            try
            {
                // Close/stop camera objects on camera thread (same thread as callbacks)
                var session = _Session;
                var device = _Device;

                await RunOnCameraThreadAsync(() =>
                {
                    if (session != null)
                    {
                        AndroidCamera2.StopRepeatingSafely(session);
                        session.Close();
                    }
                    device?.Close();
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _Log.Warning("Stop: closing session failed: {0}", ex);
            }
            finally
            {
                _Session?.Dispose();
                _Session = null;
            }

            // device.Close() already executed on camera thread above; just dispose here.
            _Device?.Dispose();
            _Device = null;

            if (_Preview != null)
            {
                try
                {
                    await _Preview.StopAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _Log.Warning("Stop: preview stop failed: {0}", ex);
                }
                _Preview = null;
            }

            if (_Encoder != null)
            {
                try
                {
                    await _Encoder.StopAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _Log.Warning("Stop: encoder stop failed: {0}", ex);
                }
                _Encoder = null;
            }
        }
        finally
        {
            StopThreads();
            _Log.Information("CameraFeed stopped.");
        }
    }

    private Task RunOnCameraThreadAsync(Action action)
    {
        var h = _CameraHandler;

        if (h == null)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bool posted = h.Post(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        if (!posted)
        {
            tcs.TrySetException(new InvalidOperationException("Camera handler is not accepting work (thread shutting down)."));
        }

        return tcs.Task;
    }

    private void StartThreads()
    {
        _CameraThread ??= new HandlerThread("FoosVision.Camera2");
        if (!_CameraThread.IsAlive) _CameraThread.Start();
        _CameraHandler ??= new Handler(_CameraThread.Looper!);

        _CodecThread ??= new HandlerThread("FoosVision.MediaCodec");
        if (!_CodecThread.IsAlive) _CodecThread.Start();
        _CodecHandler ??= new Handler(_CodecThread.Looper!);

        _GlThread ??= new HandlerThread("FoosVision.GL");
        if (!_GlThread.IsAlive) _GlThread.Start();
        _GlHandler ??= new Handler(_GlThread.Looper!);
    }

    private void StopThreads()
    {
        static void Quit(HandlerThread? t)
        {
            if (t == null) return;
            try
            {
                t.QuitSafely();
            }
            catch
            { // ignore
            }

            try
            {
                // Don't join self.
                if (Java.Lang.Thread.CurrentThread() != t)
                {
                    t.Join();
                }
            }
            catch
            {
            }
        }

        Quit(_CameraThread);
        Quit(_CodecThread);
        Quit(_GlThread);

        _CameraHandler = null;
        _CodecHandler = null;
        _GlHandler = null;

        _CameraThread?.Dispose();
        _CodecThread?.Dispose();
        _GlThread?.Dispose();

        _CameraThread = null;
        _CodecThread = null;
        _GlThread = null;
    }
}
