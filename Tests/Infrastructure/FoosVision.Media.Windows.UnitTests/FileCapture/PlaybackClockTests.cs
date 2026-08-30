// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Windows.FileCapture;

namespace FoosVision.Media.Windows.UnitTests.FileCapture;

public class PlaybackClockTests
{
    [Fact]
    public void ShouldEmitDecodedFrame_returns_true_every_fourth_tick_for_120_to_30()
    {
        PlaybackClock clock = new(120, 30);

        Assert.True(clock.ShouldEmitDecodedFrame(0));
        Assert.False(clock.ShouldEmitDecodedFrame(1));
        Assert.False(clock.ShouldEmitDecodedFrame(2));
        Assert.False(clock.ShouldEmitDecodedFrame(3));
        Assert.True(clock.ShouldEmitDecodedFrame(4));
    }

    [Fact]
    public async Task DelayUntilAsync_returns_without_waiting_for_first_tick()
    {
        PlaybackClock clock = new(120, 30);
        clock.Start();

        await clock.DelayUntilAsync(0, CancellationToken.None);
    }

    [Fact]
    public void Constructor_rejects_non_divisible_frame_rates()
    {
        Assert.Throws<ArgumentException>(() => new PlaybackClock(120, 50));
    }
}
