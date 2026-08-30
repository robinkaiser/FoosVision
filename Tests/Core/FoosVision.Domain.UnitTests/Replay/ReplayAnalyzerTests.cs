// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.Replay;

public class ReplayAnalyzerTests
{
    private readonly ReplayAnalyzer _Testee = new(TableImageScale.From(TableConfig.Config));
    private readonly TableImageScale _TableImageScale = TableImageScale.From(TableConfig.Config);
    private readonly BallPossession _ShotAnchorPossession = new(Team.A, PossessionArea.ThreeBar);

    [Fact]
    public void Speed_metrics_use_recent_replay_positions_in_average_time_window_and_have_no_sign_and_are_reported_as_kmh()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500,   0,     0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1105, 450,   0,     0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(20), 1109, 410,   0,     0, true, ReplayTrackedFrameStatus.Tracked),
        ]);

        var expectedGoalSpeed = _TableImageScale.ConvertGoalAxisSpeedPxPerSToKmh(500);
        var expectedSideSpeed = _TableImageScale.ConvertSideAxisSpeedPxPerSToKmh(-5000);
        expectedSideSpeed = Math.Abs(expectedSideSpeed);

        Assert.All(analysis.Frames, frame => Assert.Equal(3, frame.Metrics.Count));

        ReplayMetric goalSpeed = GetMetric(analysis.Frames[2], ReplayAnalyzer.GoalSpeedMetricName);
        ReplayMetric sideSpeed = GetMetric(analysis.Frames[2], ReplayAnalyzer.SideSpeedMetricName);

        Assert.Equal(expectedGoalSpeed, goalSpeed.Value);
        Assert.Equal(expectedSideSpeed, sideSpeed.Value);
        Assert.Equal("km/h", goalSpeed.Unit);
        Assert.Equal("km/h", sideSpeed.Unit);
    }

    [Fact]
    public void Missing_frames_do_not_add_zero_velocity_to_speed_average()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1110, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(20),    0,   0, 0, 0, false, ReplayTrackedFrameStatus.Missing),
            Frame(Ns(30), 1130, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
        ]);

        var expectedGoalSpeed = _TableImageScale.ConvertGoalAxisSpeedPxPerSToKmh(1000);
        ReplayMetric goalSpeed = GetMetric(analysis.Frames[3], ReplayAnalyzer.GoalSpeedMetricName);

        Assert.Equal(expectedGoalSpeed, goalSpeed.Value);
    }

    [Fact]
    public void Speed_average_keeps_four_120_fps_frame_times_in_average_time_window()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(08), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(17),    0,   0, 0, 0, false, ReplayTrackedFrameStatus.Missing),
            Frame(Ns(25),    0,   0, 0, 0, false, ReplayTrackedFrameStatus.Missing),
            Frame(Ns(33), 1125, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
        ]);

        var expectedGoalSpeed = _TableImageScale.ConvertGoalAxisSpeedPxPerSToKmh(25 / 0.033);
        ReplayMetric goalSpeed = GetMetric(analysis.Frames[4], ReplayAnalyzer.GoalSpeedMetricName);

        Assert.Equal(expectedGoalSpeed, goalSpeed.Value);
    }

    [Fact]
    public void Shot_candidate_starts_via_X_velocity()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1104, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(20), 1108, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(30), 1112, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(40), 1116, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[3]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[4]));
    }

    [Fact]
    public void Shot_candidate_starts_via_Y_velocity()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1100, 504, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(20), 1100, 508, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(30), 1100, 512, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(40), 1100, 516, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[3]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[4]));
    }

    [Fact]
    public void Shot_candidate_does_not_start_due_to_low_velocity()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1101, 501, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(20), 1103, 503, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[2]));
    }

    [Fact]
    public void Shot_candidate_is_discarded_due_to_change_of_direction()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1100, 504, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(20), 1100, 504, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(30), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[3]));
    }

    [Fact]
    public void Confirmed_shot_by_leaving_anchor_possession_will_not_stop_due_to_change_of_direction()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1100, 504, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(20), 1100, 504, 0, 0, false, ReplayTrackedFrameStatus.Tracked), // Position X = 1100 will not matter here
            Frame(Ns(30), 1100, 500, 0, 0, false, ReplayTrackedFrameStatus.Tracked), // Position X = 1100 will not matter here
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(10.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(20.0, GetDuration(analysis.Frames[3]));
    }

    [Fact]
    public void Confirmed_shot_by_side_movement_will_not_stop_due_to_change_of_direction()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1100, 540, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(20), 1100, 590, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(30), 1100, 590, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
            Frame(Ns(40), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Tracked),
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(10.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(20.0, GetDuration(analysis.Frames[3]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[4]));
    }

    [Fact]
    public void Shot_duration_does_not_change_on_frames_with_missing_ball()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1104, 501, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(20), 1108, 502, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(30), 1112, 503, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(40), 1116, 504, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 30 ms
            Frame(Ns(50),    0,   0, 0, 0, false, ReplayTrackedFrameStatus.Missing) // 30 ms, no change for missing frame
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[3]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[4]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[5]));
    }

    [Fact]
    public void Shot_duration_does_not_change_on_predicted_frames_until_ball_is_observed_again()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1104, 501, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(20), 1108, 502, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(30), 1112, 503, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(40), 1116, 504, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 30 ms
            Frame(Ns(50), 1120, 505, 0, 0, true, ReplayTrackedFrameStatus.Predicted), // internally 40 ms, published 30 ms
            Frame(Ns(60), 1124, 506, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // observed again, published catches up to 50 ms
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[3]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[4]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[5]));
        Assert.Equal(50.0, GetDuration(analysis.Frames[6]));
    }

    [Fact]
    public void Shot_duration_does_not_extend_when_ball_only_remains_predicted()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500, 0, 0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1104, 501, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(20), 1108, 502, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(30), 1112, 503, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(40), 1116, 504, 0, 0, true, ReplayTrackedFrameStatus.Tracked), // 30 ms
            Frame(Ns(50), 1120, 505, 0, 0, true, ReplayTrackedFrameStatus.Predicted),
            Frame(Ns(60), 1124, 506, 0, 0, true, ReplayTrackedFrameStatus.Predicted),
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[3]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[4]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[5]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[6]));
    }

    [Fact]
    public void Shot_candidate_duration_is_suppressed_until_display_threshold()
    {
        ReplayAnalysis analysis = _Testee.Analyze(
        [
            Frame(Ns(00), 1100, 500,   0,   0, true, ReplayTrackedFrameStatus.Anchor),
            Frame(Ns(10), 1104, 501, 400, 100, true, ReplayTrackedFrameStatus.Tracked), // 0 ms
            Frame(Ns(20), 1105, 502, 100, 100, true, ReplayTrackedFrameStatus.Tracked), // 10 ms
            Frame(Ns(30), 1106, 503, 100, 100, true, ReplayTrackedFrameStatus.Tracked), // 20 ms
            Frame(Ns(40), 1107, 504, 100, 100, true, ReplayTrackedFrameStatus.Tracked), // 30 ms > 25 ms, no longer suppressed
        ]);

        Assert.Equal(0.0, GetDuration(analysis.Frames[0]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[1]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[2]));
        Assert.Equal(0.0, GetDuration(analysis.Frames[3]));
        Assert.Equal(30.0, GetDuration(analysis.Frames[4]));
    }

    [Theory]
    [InlineData(ReplayTrackedFrameStatus.Anchor, true)]
    [InlineData(ReplayTrackedFrameStatus.Tracked, true)]
    [InlineData(ReplayTrackedFrameStatus.Predicted, true)]
    [InlineData(ReplayTrackedFrameStatus.Missing, false)]
    public void Analysis_frame_contains_ball_position_unless_tracking_frame_is_missing(
        ReplayTrackedFrameStatus status,
        bool expectedBallPosition)
    {
        ReplayAnalysis analysis = _Testee.Analyze([Frame(0, 0, 0, 0, 0, true, status)]);

        ReplayAnalysisFrame frame = Assert.Single(analysis.Frames);

        Assert.Equal(expectedBallPosition, frame.BallPosition.IsSome);
    }

    private static long Ns(int ms)
        => ms * 1_000_000;

    private ReplayTrackedFrame Frame(
       long timeNs,
       int posX,
       int posY,
       double veloX,
       double veloY,
       bool isInShotAnchorPossession,
       ReplayTrackedFrameStatus status)
       => new(
           timeNs,
           new Point(posX, posY),
           isInShotAnchorPossession ? _ShotAnchorPossession : BallPossession.None,
           status,
           new Vector2(veloX, veloY));

    private static ReplayMetric GetMetric(ReplayAnalysisFrame frame, string name)
        => Assert.Single(frame.Metrics, metric => metric.Name == name);

    private static double GetDuration(ReplayAnalysisFrame frame)
        => GetMetric(frame, ReplayAnalyzer.ShotDurationMetricName).Value;
}
