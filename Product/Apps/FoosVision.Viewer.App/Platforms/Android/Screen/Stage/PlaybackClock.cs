// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Stage;

public class PlaybackClock
{
    private long _StartTimestampNs;
    private long _StartTime;
    private bool _Started;

    public async Task DelayUntilDueAsync(long timestampNs, double speed, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(speed, 0);

        if (!_Started)
        {
            _StartTimestampNs = timestampNs;
            _StartTime = Stopwatch.GetTimestamp();
            _Started = true;
            return;
        }

        long targetElapsedNs = (long)((timestampNs - _StartTimestampNs) / speed);
        if (targetElapsedNs <= 0)
        {
            return;
        }

        while (true)
        {
            long elapsedNs = GetElapsedNs();
            long remainingNs = targetElapsedNs - elapsedNs;
            if (remainingNs <= 0)
            {
                return;
            }

            TimeSpan delay = TimeSpan.FromTicks(Math.Max(1, remainingNs / 100));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task DelayUntilDueAsync(long timestampNs, CancellationToken cancellationToken)
    {
        return DelayUntilDueAsync(timestampNs, 1D, cancellationToken);
    }

    public void Reset()
    {
        _StartTimestampNs = 0;
        _StartTime = 0;
        _Started = false;
    }

    private long GetElapsedNs()
    {
        return Stopwatch.GetElapsedTime(_StartTime).Ticks * TimeSpan.NanosecondsPerTick;
    }
}
