// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Replay;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Replay;

public class ReplayAnalysisMapperTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Map_only_draws_current_ball_position_when_present(bool hasBallPosition)
    {
        ReplayAnalysis analysis = new(
            [
                new ReplayAnalysisFrame(
                    1_000_000_000,
                    hasBallPosition ? Option<Point>.Some(new Point(960, 540)) : Option<Point>.None(),
                    hasBallPosition ? new BallPossession(Team.A, PossessionArea.FiveBar) : BallPossession.None,
                    []),
            ]);

        var state = ReplayAnalysisMapper.Map(analysis, 1_000_000_000);

        Assert.Equal(hasBallPosition, state.BallPosition is not null);
    }

    [Fact]
    public void Map_uses_current_frame_metrics()
    {
        ReplayAnalysis analysis = new(
            [
                Frame(1_000_000_000, []),
                Frame(
                    2_000_000_000,
                    [
                        new ReplayMetric(ReplayAnalyzer.GoalSpeedMetricName, 18.0, "km/h"),
                        new ReplayMetric(ReplayAnalyzer.SideSpeedMetricName, 4.0, "km/h"),
                        new ReplayMetric(ReplayAnalyzer.ShotDurationMetricName, 100.0, "ms"),
                    ]),
                Frame(
                    3_000_000_000,
                    [
                        new ReplayMetric(ReplayAnalyzer.GoalSpeedMetricName, 54.0, "km/h"),
                        new ReplayMetric(ReplayAnalyzer.SideSpeedMetricName, 8.0, "km/h"),
                        new ReplayMetric(ReplayAnalyzer.ShotDurationMetricName, 200.0, "ms"),
                    ]),
            ]);

        var state = ReplayAnalysisMapper.Map(analysis, 2_000_000_000);

        Assert.Collection(
            state.Metrics,
            metric =>
            {
                Assert.Equal(ReplayAnalyzer.GoalSpeedMetricName, metric.Name);
                Assert.Equal(18.0, metric.Value, precision: 3);
                Assert.Equal("km/h", metric.Unit);
            },
            metric =>
            {
                Assert.Equal(ReplayAnalyzer.SideSpeedMetricName, metric.Name);
                Assert.Equal(4.0, metric.Value, precision: 3);
                Assert.Equal("km/h", metric.Unit);
            },
            metric =>
            {
                Assert.Equal(ReplayAnalyzer.ShotDurationMetricName, metric.Name);
                Assert.Equal(100.0, metric.Value, precision: 3);
                Assert.Equal("ms", metric.Unit);
            });
    }

    [Fact]
    public void Map_shows_anchor_possession_time_while_ball_stays_on_anchor_possession()
    {
        ReplayAnalysis analysis = new(
            [
                Frame(1_000_000_000, new Point(800, 540)),
                Frame(1_250_000_000, new Point(810, 540)),
            ]);
        ReplayPossessionOverlay possessionOverlay = new(
            new BallPossession(Team.A, PossessionArea.FiveBar),
            1_000_000_000,
            9250);

        var state = ReplayAnalysisMapper.Map(analysis, 1_250_000_000, possessionOverlay);

        Assert.Equal(Team.A, state.PossessingTeam);
        Assert.Equal(PossessionArea.FiveBar, state.PossessionArea);
        Assert.Equal(9500, state.PossessionTimeMs);
    }

    [Fact]
    public void Map_freezes_anchor_possession_time_after_ball_leaves_anchor_possession()
    {
        ReplayAnalysis analysis = new(
            [
                Frame(1_000_000_000, new Point(800, 540)),
                Frame(1_125_000_000, new Point(810, 540)),
                Frame(1_250_000_000, new Point(1000, 540), new BallPossession(Team.B, PossessionArea.FiveBar)),
                Frame(1_375_000_000, new Point(810, 540)),
            ]);
        ReplayPossessionOverlay possessionOverlay = new(
            new BallPossession(Team.A, PossessionArea.FiveBar),
            1_000_000_000,
            9250);

        var state = ReplayAnalysisMapper.Map(analysis, 1_375_000_000, possessionOverlay);

        Assert.Equal(Team.A, state.PossessingTeam);
        Assert.Equal(PossessionArea.FiveBar, state.PossessionArea);
        Assert.Equal(9375, state.PossessionTimeMs);
    }

    [Fact]
    public void Map_freezes_anchor_possession_time_when_current_ball_position_is_missing()
    {
        ReplayAnalysis analysis = new(
            [
                Frame(1_000_000_000, new Point(800, 540)),
                Frame(1_125_000_000, new Point(810, 540)),
                new ReplayAnalysisFrame(
                    1_250_000_000,
                    Option<Point>.None(),
                    BallPossession.None,
                    []),
            ]);
        ReplayPossessionOverlay possessionOverlay = new(
            new BallPossession(Team.A, PossessionArea.FiveBar),
            1_000_000_000,
            9250);

        var state = ReplayAnalysisMapper.Map(analysis, 1_250_000_000, possessionOverlay);

        Assert.Equal(Team.A, state.PossessingTeam);
        Assert.Equal(PossessionArea.FiveBar, state.PossessionArea);
        Assert.Equal(9375, state.PossessionTimeMs);
    }

    [Fact]
    public void Map_treats_goalie_and_two_bar_as_same_defense_possession()
    {
        ReplayAnalysis analysis = new(
            [
                Frame(1_000_000_000, new Point(200, 540), new BallPossession(Team.A, PossessionArea.Defense)),
                Frame(1_250_000_000, new Point(400, 540), new BallPossession(Team.A, PossessionArea.Defense)),
            ]);
        ReplayPossessionOverlay possessionOverlay = new(
            new BallPossession(Team.A, PossessionArea.Defense),
            1_000_000_000,
            9250);

        var state = ReplayAnalysisMapper.Map(analysis, 1_250_000_000, possessionOverlay);

        Assert.Equal(Team.A, state.PossessingTeam);
        Assert.Equal(PossessionArea.Defense, state.PossessionArea);
        Assert.Equal(9500, state.PossessionTimeMs);
    }

    private static ReplayAnalysisFrame Frame(long timeNs, IReadOnlyList<ReplayMetric> metrics)
        => new(
            timeNs,
            Option<Point>.Some(new Point(960, 540)),
            new BallPossession(Team.A, PossessionArea.FiveBar),
            metrics);

    private static ReplayAnalysisFrame Frame(long timeNs, Point ballPosition)
        => Frame(timeNs, ballPosition, new BallPossession(Team.A, PossessionArea.FiveBar));

    private static ReplayAnalysisFrame Frame(long timeNs, Point ballPosition, BallPossession possession)
        => new(timeNs, Option<Point>.Some(ballPosition), possession, []);
}
