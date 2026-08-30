// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Overlays;

public class TrackingOverlayProjectorTests
{
    [Fact]
    public void Project_omits_metrics_until_table_configuration_available()
    {
        TrackingOverlayProjector testee = new();

        TrackingOverlayState state = testee.Project(CreateTrackingFrame(1, 1_000_000_000, 1000, 2000));

        Assert.Empty(state.Metrics);
    }

    [Fact]
    public void Project_reports_raw_live_speed_below_hold_threshold()
    {
        TableConfiguration tableConfiguration = CreateTableConfiguration();
        TableImageScale scale = TableImageScale.From(tableConfiguration);
        TrackingOverlayProjector testee = new();

        testee.UpdateTableConfiguration(tableConfiguration);
        _ = testee.Project(CreateTrackingFrame(1, 1_000_000_000, -100, 100));
        TrackingOverlayState state = testee.Project(CreateTrackingFrame(2, 1_100_000_000, 200, -200));

        double expectedGoalSpeed = scale.ConvertGoalAxisSpeedPxPerSToKmh(200);
        double expectedSideSpeed = scale.ConvertSideAxisSpeedPxPerSToKmh(200);

        Assert.Collection(
            state.Metrics,
            metric =>
            {
                Assert.Equal(ReplayAnalyzer.GoalSpeedMetricName, metric.Name);
                Assert.Equal(expectedGoalSpeed, metric.Value, precision: 3);
                Assert.Equal("km/h", metric.Unit);
            },
            metric =>
            {
                Assert.Equal(ReplayAnalyzer.SideSpeedMetricName, metric.Name);
                Assert.Equal(expectedSideSpeed, metric.Value, precision: 3);
                Assert.Equal("km/h", metric.Unit);
            });
    }

    [Fact]
    public void Project_holds_speed_above_threshold_for_500_ms()
    {
        TableConfiguration tableConfiguration = CreateTableConfiguration();
        TableImageScale scale = TableImageScale.From(tableConfiguration);
        TrackingOverlayProjector testee = new();

        testee.UpdateTableConfiguration(tableConfiguration);
        _ = testee.Project(CreateTrackingFrame(1, 1_000_000_000, 1000, 2000));
        TrackingOverlayState held = testee.Project(CreateTrackingFrame(2, 1_400_000_000, 100, 200));
        TrackingOverlayState live = testee.Project(CreateTrackingFrame(3, 1_501_000_000, 100, 200));

        Assert.Equal(scale.ConvertGoalAxisSpeedPxPerSToKmh(1000), held.Metrics[0].Value, precision: 3);
        Assert.Equal(scale.ConvertSideAxisSpeedPxPerSToKmh(2000), held.Metrics[1].Value, precision: 3);
        Assert.Equal(scale.ConvertGoalAxisSpeedPxPerSToKmh(100), live.Metrics[0].Value, precision: 3);
        Assert.Equal(scale.ConvertSideAxisSpeedPxPerSToKmh(200), live.Metrics[1].Value, precision: 3);
    }

    [Fact]
    public void Project_raises_held_metric_and_resets_hold_when_either_axis_is_above_threshold()
    {
        TableConfiguration tableConfiguration = CreateTableConfiguration();
        TableImageScale scale = TableImageScale.From(tableConfiguration);
        TrackingOverlayProjector testee = new();

        testee.UpdateTableConfiguration(tableConfiguration);
        _ = testee.Project(CreateTrackingFrame(1, 1_000_000_000, 1500, 400));
        TrackingOverlayState raised = testee.Project(CreateTrackingFrame(2, 1_400_000_000, 200, 1000));
        TrackingOverlayState stillHeld = testee.Project(CreateTrackingFrame(3, 1_850_000_000, 100, 100));

        Assert.Equal(scale.ConvertGoalAxisSpeedPxPerSToKmh(1500), raised.Metrics[0].Value, precision: 3);
        Assert.Equal(scale.ConvertSideAxisSpeedPxPerSToKmh(1000), raised.Metrics[1].Value, precision: 3);
        Assert.Equal(scale.ConvertGoalAxisSpeedPxPerSToKmh(1500), stillHeld.Metrics[0].Value, precision: 3);
        Assert.Equal(scale.ConvertSideAxisSpeedPxPerSToKmh(1000), stillHeld.Metrics[1].Value, precision: 3);
    }

    [Fact]
    public void Project_starts_new_metric_hold_after_previous_hold_expired()
    {
        TableConfiguration tableConfiguration = CreateTableConfiguration();
        TableImageScale scale = TableImageScale.From(tableConfiguration);
        TrackingOverlayProjector testee = new();

        testee.UpdateTableConfiguration(tableConfiguration);
        _ = testee.Project(CreateTrackingFrame(1, 1_000_000_000, 2000, 2000));
        TrackingOverlayState state = testee.Project(CreateTrackingFrame(2, 1_600_000_000, 1000, 400));

        Assert.Equal(scale.ConvertGoalAxisSpeedPxPerSToKmh(1000), state.Metrics[0].Value, precision: 3);
        Assert.Equal(scale.ConvertSideAxisSpeedPxPerSToKmh(400), state.Metrics[1].Value, precision: 3);
    }

    [Fact]
    public void Project_resets_metric_hold_when_frame_order_restarts()
    {
        TableConfiguration tableConfiguration = CreateTableConfiguration();
        TableImageScale scale = TableImageScale.From(tableConfiguration);
        TrackingOverlayProjector testee = new();

        testee.UpdateTableConfiguration(tableConfiguration);
        _ = testee.Project(CreateTrackingFrame(10, 1_000_000_000, 1000, 1000));
        _ = testee.Project(CreateTrackingFrame(11, 1_100_000_000, 2000, 2000));
        TrackingOverlayState state = testee.Project(CreateTrackingFrame(1, 900_000_000, 100, 200));

        Assert.Equal(scale.ConvertGoalAxisSpeedPxPerSToKmh(100), state.Metrics[0].Value, precision: 3);
        Assert.Equal(scale.ConvertSideAxisSpeedPxPerSToKmh(200), state.Metrics[1].Value, precision: 3);
    }

    private static TrackingFrameMessage CreateTrackingFrame(
        ulong frameId,
        long timestampNs,
        double velocityX,
        double velocityY)
    {
        return new TrackingFrameMessage
        {
            FrameId = frameId,
            TimestampNs = timestampNs,
            IsBallFound = true,
            BallPosition = new PointMessage { X = 960, Y = 540 },
            BallVelocityPxPerS = new VectorMessage { X = velocityX, Y = velocityY },
        };
    }

    private static TableConfiguration CreateTableConfiguration()
    {
        Dictionary<BarType, Bar> bars = [];

        foreach (BarType type in Enum.GetValues<BarType>())
        {
            int x = 100 + ((int)type * 200);
            bars.Add(
                type,
                new Bar(
                    type,
                    new Line(new Point(x - 20, 0), new Point(x - 20, 1080)),
                    new Line(new Point(x, 0), new Point(x, 1080)),
                    new Line(new Point(x + 20, 0), new Point(x + 20, 1080))));
        }

        return new TableConfiguration(
            new PlayingField(
                new Trapezium(
                    new Point(120, 120),
                    new Point(1480, 120),
                    new Point(120, 780),
                    new Point(1480, 780)),
                new TableBars(
                    bars[BarType.A1],
                    bars[BarType.A2],
                    bars[BarType.B3],
                    bars[BarType.A5],
                    bars[BarType.B5],
                    bars[BarType.A3],
                    bars[BarType.B2],
                    bars[BarType.B1]),
                []),
            new PlayerColors(0xFFFF0000, 0xFF0000FF),
            BallColor.Unknown);
    }
}
