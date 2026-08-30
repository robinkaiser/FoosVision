// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Metrics;

namespace FoosVision.Viewer.App.Screen.Stage;

public class FrameRatePublisher : IDisposable
{
    private readonly object _Sync = new();
    private readonly SlidingFrameRateCounter _FrameRateCounter;
    private readonly Timer _PublishTimer;
    private readonly Func<DateTimeOffset> _UtcNow;
    private double? _FrameRate;
    private int _Disposed;

    public FrameRatePublisher(
        TimeSpan window,
        TimeSpan publishInterval,
        Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(publishInterval, TimeSpan.Zero);

        _FrameRateCounter = new SlidingFrameRateCounter(window);
        _UtcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _PublishTimer = new Timer(_ => RefreshFrameRate(), null, publishInterval, publishInterval);
    }

    public event Action<double?>? FrameRateChanged;

    public void RecordFrame()
    {
        lock (_Sync)
        {
            _FrameRateCounter.Record(_UtcNow());
        }
    }

    public void Reset()
    {
        lock (_Sync)
        {
            _FrameRateCounter.Reset();
        }

        UpdateFrameRate(null);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Interlocked.Exchange(ref _Disposed, 1) != 0)
        {
            return;
        }

        _PublishTimer.Dispose();
    }

    private void RefreshFrameRate()
    {
        if (Interlocked.CompareExchange(ref _Disposed, 0, 0) != 0)
        {
            return;
        }

        double? frameRate;
        lock (_Sync)
        {
            frameRate = _FrameRateCounter.GetFramesPerSecond(_UtcNow());
        }

        UpdateFrameRate(frameRate);
    }

    private void UpdateFrameRate(double? frameRate)
    {
        double? roundedFrameRate = frameRate.HasValue
            ? Math.Round(frameRate.Value, 1, MidpointRounding.AwayFromZero)
            : null;

        if (_FrameRate == roundedFrameRate)
        {
            return;
        }

        _FrameRate = roundedFrameRate;
        FrameRateChanged?.Invoke(roundedFrameRate);
    }
}
