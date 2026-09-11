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
    private static readonly TimeSpan _ProcessFpsWindow = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _ProcessFpsPublishInterval = TimeSpan.FromMilliseconds(500);
    private static readonly Source _Log = new("FrameProcessingLoop");

    private readonly IFrameFeed _FrameFeed;
    private readonly IFrameProcessor _FrameProcessor;
    private readonly Func<long> _GetTimestamp;
    private readonly IntervalMetric? _AcceptedInterval;
    private readonly IntervalMetric? _ProcessStartInterval;
    private readonly DurationMetric? _ProcessDuration;
    private readonly Lock _ProcessFpsSync = new();
    private readonly Queue<long> _ProcessFrameTimestamps = [];
    private readonly long _ProcessFpsWindowTicks;

    private readonly ConcurrentQueue<IFrameHandle> _FramesToBeProcessed = new();
    private readonly SemaphoreSlim _WorkAvailable = new(0);

    private CancellationTokenSource? _WorkerCts;
    private Task? _WorkerTask;
    private Timer? _ProcessFpsTimer;
    private long? _ProcessFpsStartedAt;
    private double? _ProcessFramesPerSecond;
    private bool _Running;

    public FrameProcessingLoop(
        IFrameFeed frameFeed,
        IFrameProcessor frameProcessor,
        RuntimeMetricsOptions? runtimeMetricsOptions = null,
        Func<long>? getTimestamp = null,
        TimeSpan? processFpsWindow = null,
        TimeSpan? processFpsPublishInterval = null)
    {
        _FrameFeed = frameFeed;
        _FrameProcessor = frameProcessor;
        _GetTimestamp = getTimestamp ?? Stopwatch.GetTimestamp;
        ProcessFpsPublishInterval = processFpsPublishInterval ?? _ProcessFpsPublishInterval;
        _ProcessFpsWindowTicks = ToStopwatchTicks(processFpsWindow ?? _ProcessFpsWindow);

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ProcessFpsPublishInterval, TimeSpan.Zero);

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

    public event Action<double?>? ProcessFramesPerSecondChanged;

    private TimeSpan ProcessFpsPublishInterval { get; }

    public void Start()
    {
        if (_Running) return;

        _Running = true;
        ResetProcessFrameRate();
        _WorkerCts = new CancellationTokenSource();
        _FrameFeed.FrameReady += OnFrameReady;

        _ProcessFpsTimer = new Timer(
            _ => RefreshProcessFrameRate(),
            null,
            ProcessFpsPublishInterval,
            ProcessFpsPublishInterval);
        _WorkerTask = Task.Run(() => WorkerLoop(_WorkerCts.Token));
    }

    public void Stop()
    {
        if (!_Running) return;

        _Running = false;
        _FrameFeed.FrameReady -= OnFrameReady;

        _WorkerCts?.Cancel();
        _WorkAvailable.Release();
        _ProcessFpsTimer?.Dispose();
        _ProcessFpsTimer = null;

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

        ResetProcessFrameRate();
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
                    RecordProcessedFrame();
                    continue;
                }

                long started = Stopwatch.GetTimestamp();
                try
                {
                    await _FrameProcessor.Process(nextFrame, token);
                    RecordProcessedFrame();
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

    private void RecordProcessedFrame()
    {
        lock (_ProcessFpsSync)
        {
            long timestamp = _GetTimestamp();
            _ProcessFpsStartedAt ??= timestamp;
            _ProcessFrameTimestamps.Enqueue(timestamp);
            PruneProcessFrameTimestamps(timestamp);
        }
    }

    private void RefreshProcessFrameRate()
    {
        double? framesPerSecond;
        lock (_ProcessFpsSync)
        {
            long now = _GetTimestamp();
            PruneProcessFrameTimestamps(now);

            framesPerSecond = _ProcessFpsStartedAt is null || now - _ProcessFpsStartedAt < _ProcessFpsWindowTicks
                ? null
                : _ProcessFrameTimestamps.Count * (double)Stopwatch.Frequency / _ProcessFpsWindowTicks;
        }

        PublishProcessFrameRate(framesPerSecond);
    }

    private void ResetProcessFrameRate()
    {
        lock (_ProcessFpsSync)
        {
            _ProcessFrameTimestamps.Clear();
            _ProcessFpsStartedAt = null;
        }

        PublishProcessFrameRate(null);
    }

    private void PruneProcessFrameTimestamps(long now)
    {
        long cutoff = now - _ProcessFpsWindowTicks;

        while (_ProcessFrameTimestamps.TryPeek(out long timestamp) && timestamp < cutoff)
        {
            _ProcessFrameTimestamps.Dequeue();
        }
    }

    private void PublishProcessFrameRate(double? framesPerSecond)
    {
        double? roundedFramesPerSecond = framesPerSecond.HasValue
            ? Math.Round(framesPerSecond.Value, 1, MidpointRounding.AwayFromZero)
            : null;

        if (_ProcessFramesPerSecond == roundedFramesPerSecond)
        {
            return;
        }

        _ProcessFramesPerSecond = roundedFramesPerSecond;
        ProcessFramesPerSecondChanged?.Invoke(roundedFramesPerSecond);
    }

    private static long ToStopwatchTicks(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        return (long)Math.Ceiling(duration.TotalSeconds * Stopwatch.Frequency);
    }
}
