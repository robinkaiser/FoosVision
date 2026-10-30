// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.CalibrationCore.Services;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.CalibrationCore;

public class TableUpdateScheduleTests
{
    [Fact]
    public void Table_interval_and_mutual_exclusion_are_preserved()
    {
        TableUpdateSchedule schedule = new();
        Assert.False(schedule.RequestTableUpdate(FrameAt(9999)));
        Assert.True(schedule.RequestTableUpdate(FrameAt(10000)));
        Assert.False(Scene(schedule, 11000, 100));
        Assert.False(schedule.RequestTableUpdate(FrameAt(20000)));
        schedule.CompleteTableUpdate();
        Assert.True(Scene(schedule, 20000, 100));
        Assert.False(schedule.RequestTableUpdate(FrameAt(20000)));
        schedule.CompleteTableSceneUpdate();
        Assert.True(schedule.RequestTableUpdate(FrameAt(20000)));
    }

    [Fact]
    public void Scene_interval_and_distance_include_their_boundaries()
    {
        TableUpdateSchedule schedule = new();
        Assert.False(Scene(schedule, 499, 50));
        Assert.False(Scene(schedule, 500, 49));
        Assert.True(Scene(schedule, 500, 50));
        schedule.CompleteTableSceneUpdate();
        Assert.False(Scene(schedule, 999, 100));
        Assert.False(Scene(schedule, 1000, 99));
        Assert.True(Scene(schedule, 1000, 100));
    }

    [Theory]
    [InlineData(false, TrackingStatus.Observed, TrackingConfidence.High)]
    [InlineData(true, TrackingStatus.Predicted, TrackingConfidence.High)]
    [InlineData(true, TrackingStatus.Observed, TrackingConfidence.Average)]
    [InlineData(true, TrackingStatus.Observed, TrackingConfidence.Low)]
    public void Scene_requires_found_observed_high_confidence_ball(bool found, TrackingStatus status, TrackingConfidence confidence)
    {
        TableUpdateSchedule schedule = new();
        Frame frame = FrameAt(500);
        TrackedBall ball = new(1, frame, new Point(100, 0), confidence, status, default);
        Assert.False(schedule.RequestTableSceneUpdate(frame, found, ball.Position, [ball]));
        Assert.False(schedule.RequestTableSceneUpdate(frame, true, new Point(200, 0), [ball]));
    }

    [Fact]
    public void New_session_schedule_has_no_previous_progress_or_position()
    {
        TableUpdateSchedule previous = new();
        Assert.True(Scene(previous, 500, 50));
        TableUpdateSchedule next = new();
        Assert.True(Scene(next, 500, 50));
    }

    private static Frame FrameAt(long ms) => new((ulong)ms, ms * 1_000_000);

    private static bool Scene(TableUpdateSchedule schedule, long ms, double x)
    {
        Frame frame = FrameAt(ms);
        TrackedBall ball = new(1, frame, new Point(x, 0), TrackingConfidence.High, TrackingStatus.Observed, default);
        return schedule.RequestTableSceneUpdate(frame, true, ball.Position, [ball]);
    }
}
