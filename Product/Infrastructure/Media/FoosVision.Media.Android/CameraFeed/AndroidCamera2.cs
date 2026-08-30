// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using Android.Content;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.OS;
using Android.Runtime;
using Android.Views;
using FoosVision.Common.Logging;
using Java.Util.Concurrent;
using ARange = Android.Util.Range;

namespace FoosVision.Media.Android.CameraFeed;

internal static class AndroidCamera2
{
    private static readonly Source _Log = new("Media.Android.Camera2");

    public static Task<CameraDevice> OpenAsync(Context appContext, Handler handler, string cameraId)
    {
        var cm = (CameraManager)appContext.GetSystemService(Context.CameraService)!;

        var tcs = new TaskCompletionSource<CameraDevice>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            cm.OpenCamera(cameraId, new OpenCallback(tcs), handler);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }

    public static Task<CameraCaptureSession> CreateHighSpeedSessionAsync(
        Handler handler,
        CameraDevice device,
        Surface previewSurface,
        Surface recordSurface)
    {
        var tcs = new TaskCompletionSource<CameraCaptureSession>(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            var outputs = new List<OutputConfiguration>
            {
                CreateOutputConfiguration(previewSurface),
                CreateOutputConfiguration(recordSurface),
            };

            var executor = new HandlerPostingExecutor(handler);

            var sessionConfig = new SessionConfiguration(
                (int)SessionType.HighSpeed,
                outputs,
                executor,
                new SessionCallback(tcs));

            device.CreateCaptureSession(sessionConfig);
        }
        catch (Exception ex)
        {
            tcs.TrySetException(ex);
        }

        return tcs.Task;
    }

    public static void StartHighSpeedRepeating(
        CameraCaptureSession session,
        CameraDevice device,
        Surface previewSurface,
        Surface recordSurface,
        int slowMoFps)
    {
        if (session is not CameraConstrainedHighSpeedCaptureSession hs)
        {
            throw new InvalidOperationException("CaptureSession is not a constrained high-speed session.");
        }

        var builder = device.CreateCaptureRequest(CameraTemplate.Record);
        builder.AddTarget(previewSurface);
        builder.AddTarget(recordSurface);

        var lo = Java.Lang.Integer.ValueOf(slowMoFps);
        var hi = Java.Lang.Integer.ValueOf(slowMoFps);
        builder.Set(CaptureRequest.ControlAeTargetFpsRange!, new ARange(lo, hi));

        var request = builder.Build();
        var burst = hs.CreateHighSpeedRequestList(request);

        hs.SetRepeatingBurst(burst, null, null);
    }

    public static void StopRepeatingSafely(CameraCaptureSession session)
    {
        TryIgnore(session.StopRepeating);
        TryIgnore(session.AbortCaptures);
    }

    private static OutputConfiguration CreateOutputConfiguration(Surface surface)
    {
        OutputConfiguration output = new(surface);
        output.TimestampBase = (int)TimestampBase.Sensor;
        return output;
    }

    private sealed class OpenCallback : CameraDevice.StateCallback
    {
        private readonly TaskCompletionSource<CameraDevice> _Tcs;
        public OpenCallback(TaskCompletionSource<CameraDevice> tcs) => _Tcs = tcs;

        public override void OnOpened(CameraDevice camera) => _Tcs.TrySetResult(camera);

        public override void OnDisconnected(CameraDevice camera)
        {
            TryIgnore(camera.Close);
            _Tcs.TrySetException(new InvalidOperationException("Camera disconnected."));
        }

        public override void OnError(CameraDevice camera, [GeneratedEnum] CameraError error)
        {
            TryIgnore(camera.Close);
            _Tcs.TrySetException(new InvalidOperationException($"Camera error: {error}."));
        }
    }

    private sealed class SessionCallback : CameraCaptureSession.StateCallback
    {
        private readonly TaskCompletionSource<CameraCaptureSession> _Tcs;
        public SessionCallback(TaskCompletionSource<CameraCaptureSession> tcs) => _Tcs = tcs;

        public override void OnConfigured(CameraCaptureSession session)
        {
            _Log.Information("CaptureSession configured: {0}", session.GetType().Name);
            _Tcs.TrySetResult(session);
        }

        public override void OnConfigureFailed(CameraCaptureSession session)
        {
            _Tcs.TrySetException(new InvalidOperationException("CaptureSession configuration failed."));
        }

        public override void OnClosed(CameraCaptureSession session)
        {
            base.OnClosed(session);
            _Log.Information("CaptureSession closed.");
        }
    }

    private sealed class HandlerPostingExecutor : Java.Lang.Object, IExecutor
    {
        private readonly Handler _Handler;

        public HandlerPostingExecutor(Handler handler) => _Handler = handler;

        public void Execute(Java.Lang.IRunnable? command)
        {
            if (command == null) return;

            if (!_Handler.Post(command))
            {
                throw new RejectedExecutionException(_Handler + " is shutting down");
            }
        }
    }

    private static void TryIgnore(Action action)
    {
        try
        {
            action();
        }
        catch
        {   // Ignore
        }
    }
}
