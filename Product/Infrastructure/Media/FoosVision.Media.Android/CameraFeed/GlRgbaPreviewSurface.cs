// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using System.Runtime.InteropServices;
using Android.OS;
using Android.Views;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Media.Android.Common;
using FoosVision.Media.Core.DecodedFrames;

namespace FoosVision.Media.Android.CameraFeed;

internal class GlRgbaPreviewSurface : IGlRgbaFrameSink
{
    private static readonly Source _Log = new("GlRgbaPreviewSurface");
    private static readonly Source _MetricsLog = new("Media.Android.GlRgbaPreviewSurface.Metrics");

    private readonly IFrameSink _FrameSink;
    private readonly DurationMetric? _MarshalCopyTiming;
    private GlRgbaSurfaceFrameReader? _Reader;

    private GlRgbaPreviewSurface(IFrameSink frameSink, RuntimeMetricsOptions runtimeMetricsOptions)
    {
        _FrameSink = frameSink;

        if (runtimeMetricsOptions.Enabled)
        {
            TimeSpan reportInterval = runtimeMetricsOptions.GetReportInterval();

            _MarshalCopyTiming = new DurationMetric(runtimeMetricsOptions.CreateMetricName("Marshal.Copy"), _MetricsLog, reportInterval);
        }
    }

    public Surface Surface => _Reader?.Surface ?? throw new InvalidOperationException("Preview surface not initialized.");

    public static async Task<GlRgbaPreviewSurface> CreateAsync(
        Handler handler,
        int width,
        int height,
        IFrameSink frameSink,
        RuntimeMetricsOptions runtimeMetricsOptions)
    {
        GlRgbaPreviewSurface preview = new(frameSink, runtimeMetricsOptions);
        preview._Reader = await GlRgbaSurfaceFrameReader.CreateAsync(
            handler,
            width,
            height,
            preview,
            runtimeMetricsOptions).ConfigureAwait(false);
        return preview;
    }

    public void Start()
    {
        _Reader?.Start();
    }

    public Task StopAsync()
        => _Reader?.StopAsync() ?? Task.CompletedTask;

    public void OnFrameAvailable(long timestampNs, nint bufferAddress, int bufferLength)
    {
        IProducerFrameHandle lease = _FrameSink.AcquireForWrite();
        byte[] outputBuffer = lease.BufferRGBA8888;

        if (outputBuffer.Length == 0)
        {
            _Log.Warning("OnFrameAvailable: Out of frame buffers");
            return;
        }

        if (bufferLength > outputBuffer.Length)
        {
            _Log.Warning("OnFrameAvailable: Frame buffer too small. Required {0}, available {1}.", bufferLength, outputBuffer.Length);
            return;
        }

        CopyFrameBuffer(bufferAddress, outputBuffer, bufferLength);

        lease.MarkWritten(timestampNs);
    }

    private void CopyFrameBuffer(nint source, byte[] destination, int length)
    {
        DurationMetric? timing = _MarshalCopyTiming;

        if (timing == null)
        {
            Marshal.Copy(source, destination, 0, length);
            return;
        }

        long started = Stopwatch.GetTimestamp();
        Marshal.Copy(source, destination, 0, length);
        timing.RecordElapsed(started);
    }
}
