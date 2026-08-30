// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Metrics;

namespace FoosVision.Common.UnitTests.Metrics;

public class SlidingFrameRateCounterTests
{
    [Fact]
    public void GetFramesPerSecond_returns_null_until_window_is_full()
    {
        SlidingFrameRateCounter sut = new(TimeSpan.FromSeconds(3));
        DateTimeOffset start = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

        sut.Record(start);
        sut.Record(start + TimeSpan.FromSeconds(1));

        Assert.Null(sut.GetFramesPerSecond(start + TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void GetFramesPerSecond_counts_frames_inside_sliding_window()
    {
        SlidingFrameRateCounter sut = new(TimeSpan.FromSeconds(3));
        DateTimeOffset start = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

        for (int i = 0; i < 90; i++)
        {
            sut.Record(start + TimeSpan.FromSeconds(i / 30D));
        }

        double? framesPerSecond = sut.GetFramesPerSecond(start + TimeSpan.FromSeconds(3));

        Assert.Equal(30, framesPerSecond);
    }

    [Fact]
    public void GetFramesPerSecond_drops_frames_outside_sliding_window()
    {
        SlidingFrameRateCounter sut = new(TimeSpan.FromSeconds(3));
        DateTimeOffset start = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

        sut.Record(start);
        sut.Record(start + TimeSpan.FromSeconds(1));
        sut.Record(start + TimeSpan.FromSeconds(4));

        double? framesPerSecond = sut.GetFramesPerSecond(start + TimeSpan.FromSeconds(4));

        Assert.Equal(2D / 3D, framesPerSecond);
    }

    [Fact]
    public void GetFramesPerSecond_returns_zero_after_full_window_without_remaining_frames()
    {
        SlidingFrameRateCounter sut = new(TimeSpan.FromSeconds(3));
        DateTimeOffset start = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

        sut.Record(start);

        Assert.Equal(0, sut.GetFramesPerSecond(start + TimeSpan.FromSeconds(4)));
    }

    [Fact]
    public void Reset_clears_recorded_frames_and_window_readiness()
    {
        SlidingFrameRateCounter sut = new(TimeSpan.FromSeconds(3));
        DateTimeOffset now = new(2026, 5, 14, 12, 0, 0, TimeSpan.Zero);

        sut.Record(now);
        sut.Reset();

        Assert.Null(sut.GetFramesPerSecond(now + TimeSpan.FromSeconds(3)));
    }
}
