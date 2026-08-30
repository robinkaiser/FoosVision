// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;

namespace FoosVision.Media.Windows.FileCapture;

internal class PlaybackClock
{
    private readonly long _EncodedFrameDurationNs;
    private readonly int _DecodedFrameStride;
    private long _StartTime;

    public PlaybackClock(int encodedFps, int decodedFps)
    {
        if (encodedFps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(encodedFps), "Encoded FPS must be greater than zero.");
        }

        if (decodedFps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decodedFps), "Decoded FPS must be greater than zero.");
        }

        if (encodedFps % decodedFps != 0)
        {
            throw new ArgumentException("Decoded FPS must divide encoded FPS.", nameof(decodedFps));
        }

        _EncodedFrameDurationNs = TimeSpan.TicksPerSecond * 100L / encodedFps;
        _DecodedFrameStride = encodedFps / decodedFps;
    }

    public void Start()
    {
        _StartTime = Stopwatch.GetTimestamp();
    }

    public bool ShouldEmitDecodedFrame(long accessUnitIndex)
    {
        return accessUnitIndex % _DecodedFrameStride == 0;
    }

    public ValueTask DelayUntilAsync(long accessUnitIndex, CancellationToken cancellationToken)
    {
        long elapsedNs = Stopwatch.GetElapsedTime(_StartTime).Ticks * TimeSpan.NanosecondsPerTick;
        long targetNs = accessUnitIndex * _EncodedFrameDurationNs;
        long remainingNs = targetNs - elapsedNs;
        if (remainingNs <= 0)
        {
            return ValueTask.CompletedTask;
        }

        TimeSpan delay = TimeSpan.FromTicks((remainingNs + 99L) / 100L);
        return new ValueTask(Task.Delay(delay, cancellationToken));
    }
}
