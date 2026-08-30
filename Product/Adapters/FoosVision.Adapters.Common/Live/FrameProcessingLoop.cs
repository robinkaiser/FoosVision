// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Collections.Concurrent;
using System.Diagnostics;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Common.Live;

public class FrameProcessingLoop
{
    private static readonly Source _Log = new("FrameProcessingLoop");

    private readonly IFrameFeed _FrameFeed;
    private readonly IFrameProcessor _FrameProcessor;
    private readonly IntervalMetric? _AcceptedInterval;
    private readonly IntervalMetric? _ProcessStartInterval;
    private readonly DurationMetric? _ProcessDuration;

    private readonly ConcurrentQueue<IFrameHandle> _FramesToBeProcessed = new();
    private readonly SemaphoreSlim _WorkAvailable = new(0);

    private CancellationTokenSource? _WorkerCts;
    private Task? _WorkerTask;
    private bool _Running;

    public FrameProcessingLoop(
        IFrameFeed frameFeed,
        IFrameProcessor frameProcessor,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _FrameFeed = frameFeed;
        _FrameProcessor = frameProcessor;

        RuntimeMetricsOptions options = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();

        if (options.Enabled)
        {
            TimeSpan reportInterval = options.GetReportInterval();

            _AcceptedInterval = new IntervalMetric(
                options.CreateMetricName("Recorder.LiveFrame.AcceptedInterval"),
                _Log,
                reportInterval);
            _ProcessStartInterval = new IntervalMetric(
                options.CreateMetricName("Recorder.LiveFrame.ProcessStartInterval"),
                _Log,
                reportInterval);
            _ProcessDuration = new DurationMetric(
                options.CreateMetricName("Recorder.LiveFrame.ProcessDuration"),
                _Log,
                reportInterval);
        }
    }

    public void Start()
    {
        if (_Running) return;

        _Running = true;
        _WorkerCts = new CancellationTokenSource();
        _FrameFeed.FrameReady += OnFrameReady;

        _WorkerTask = Task.Run(() => WorkerLoop(_WorkerCts.Token));
    }

    public void Stop()
    {
        if (!_Running) return;

        _Running = false;
        _FrameFeed.FrameReady -= OnFrameReady;

        _WorkerCts?.Cancel();
        _WorkAvailable.Release();

        try
        {
            _WorkerTask?.Wait();
        }
        catch
        {
        }

        _WorkerCts?.Dispose();
        _WorkerCts = null;
        _WorkerTask = null;

        while (_FramesToBeProcessed.TryDequeue(out var leftover))
        {   // Release any leftover frames
            leftover.Release();
        }
    }

    private void OnFrameReady(IFrameHandle frameHandle)
    {
        if (!_Running ||
            !_FrameProcessor.ShouldProcess)
        {
            frameHandle.Release();
            return;
        }

        _AcceptedInterval?.Record();
        _FramesToBeProcessed.Enqueue(frameHandle);
        _WorkAvailable.Release();
    }

    private async Task WorkerLoop(CancellationToken token)
    {
        while (true)
        {
            try
            {
                await _WorkAvailable.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_FramesToBeProcessed.TryDequeue(out var nextFrame))
            {
                continue;
            }

            if (!_FrameProcessor.ShouldProcess)
            {
                nextFrame.Release();
                continue;
            }

            try
            {
                _ProcessStartInterval?.Record();
                DurationMetric? processDuration = _ProcessDuration;

                if (processDuration == null)
                {
                    await _FrameProcessor.Process(nextFrame, token);
                    continue;
                }

                long started = Stopwatch.GetTimestamp();
                try
                {
                    await _FrameProcessor.Process(nextFrame, token);
                }
                finally
                {
                    processDuration.RecordElapsed(started);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _Log.Error(
                    "Frame processing failed. FrameId={FrameId} TimestampNs={TimestampNs} Ex={Exception}",
                    nextFrame.Meta.Id,
                    nextFrame.Meta.TimestampNs,
                    ex.ToString());
            }
            finally
            {
                nextFrame.Release();
            }
        }
    }
}
