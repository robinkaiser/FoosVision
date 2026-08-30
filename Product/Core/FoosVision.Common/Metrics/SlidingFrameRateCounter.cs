// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Metrics;

public class SlidingFrameRateCounter
{
    private readonly TimeSpan _Window;
    private readonly Queue<DateTimeOffset> _FrameTimes = [];
    private DateTimeOffset? _StartedAt;

    public SlidingFrameRateCounter(TimeSpan window)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);

        _Window = window;
    }

    public void Record(DateTimeOffset timestamp)
    {
        _StartedAt ??= timestamp;
        _FrameTimes.Enqueue(timestamp);
        Prune(timestamp);
    }

    public double? GetFramesPerSecond(DateTimeOffset now)
    {
        Prune(now);

        if (_StartedAt is null || now - _StartedAt < _Window)
        {
            return null;
        }

        return _FrameTimes.Count / _Window.TotalSeconds;
    }

    public void Reset()
    {
        _FrameTimes.Clear();
        _StartedAt = null;
    }

    private void Prune(DateTimeOffset now)
    {
        DateTimeOffset cutoff = now - _Window;

        while (_FrameTimes.TryPeek(out DateTimeOffset timestamp) && timestamp < cutoff)
        {
            _FrameTimes.Dequeue();
        }
    }
}
