// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision.Strategies;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.TrackingCore.ReplayDecision.Strategies;

public class BallDisappearedReplayStrategyTests
{
    private const long _1s = 1000L * 1_000_000L;

    private readonly BallDisappearedReplayStrategy _Testee = new(TableConfig.Config);

    [Fact]
    public void Decide_anchor_uses_frame_500ms_before_bar_exit()
    {
        Record(0, BarType.A3, TrackingConfidence.High);
        Record(250, BarType.A3, TrackingConfidence.High);
        Record(500, BarType.A3, TrackingConfidence.High);
        Record(750, BarType.A3, TrackingConfidence.High);
        Record(1000, BarType.A3, TrackingConfidence.High, HighGoalSpeed());

        Option<ReplayAnchor> anchor = DecideLost(3 * _1s);

        Assert.True(anchor.TryGetValue(out ReplayAnchor value));
        Assert.Equal(500L * 1_000_000L, value.Frame.TimestampNs);
        Assert.Equal(Position(500), value.Position);
        Assert.Equal(new BallPossession(Team.A, PossessionArea.ThreeBar), value.Possession);
        Assert.Equal(500, value.PossessionTimeMs);
    }

    [Fact]
    public void Decide_anchor_skips_latest_low_confidence_bar_segment_when_it_is_too_short()
    {
        Record(0, BarType.A3, TrackingConfidence.Average);
        Record(500, BarType.A3, TrackingConfidence.Average);
        Record(1000, BarType.A3, TrackingConfidence.Average, HighGoalSpeed());
        Record(1250, BarType.B5, TrackingConfidence.Low);
        Record(1500, BarType.B5, TrackingConfidence.Low);
        Record(1750, BarType.B5, TrackingConfidence.Low);
        Record(2000, BarType.B5, TrackingConfidence.Low);

        Option<ReplayAnchor> anchor = DecideLost(3 * _1s);

        Assert.True(anchor.TryGetValue(out ReplayAnchor value));
        Assert.Equal(500L * 1_000_000L, value.Frame.TimestampNs);
        Assert.Equal(Position(500), value.Position);
    }

    [Fact]
    public void Decide_anchor_skips_latest_bar_segment_when_it_is_too_short()
    {
        Record(0, BarType.A3, TrackingConfidence.High);
        Record(500, BarType.A3, TrackingConfidence.High);
        Record(1000, BarType.A3, TrackingConfidence.High, HighGoalSpeed());
        Record(1500, BarType.B5, TrackingConfidence.High);
        Record(1900, BarType.B5, TrackingConfidence.High);

        Option<ReplayAnchor> anchor = DecideLost(3 * _1s);

        Assert.True(anchor.TryGetValue(out ReplayAnchor value));
        Assert.Equal(500L * 1_000_000L, value.Frame.TimestampNs);
        Assert.Equal(Position(500), value.Position);
    }

    [Fact]
    public void Decide_anchor_returns_none_without_one_second_bar_segment()
    {
        Record(0, BarType.A3, TrackingConfidence.High);
        Record(400, BarType.A3, TrackingConfidence.High);
        Record(800, BarType.B5, TrackingConfidence.High);
        Record(1200, BarType.B5, TrackingConfidence.High);

        Option<ReplayAnchor> anchor = DecideLost(3 * _1s);

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_anchor_returns_none_without_high_speed_between_anchor_and_ball_loss()
    {
        Record(0, BarType.A3, TrackingConfidence.High);
        Record(500, BarType.A3, TrackingConfidence.High);
        Record(1000, BarType.A3, TrackingConfidence.High);

        Option<ReplayAnchor> anchor = DecideLost(3 * _1s);

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_anchor_accepts_without_high_speed_when_all_candidates_between_anchor_and_ball_loss_are_in_front_of_goal()
    {
        Record(0, BarType.A3, TrackingConfidence.High, position: InFrontOfGoalPosition(0));
        Record(500, BarType.A3, TrackingConfidence.High, position: InFrontOfGoalPosition(500));
        Record(1000, BarType.A3, TrackingConfidence.High, position: InFrontOfGoalPosition(1000));

        Option<ReplayAnchor> anchor = DecideLost(3 * _1s);

        Assert.True(anchor.TryGetValue(out ReplayAnchor value));
        Assert.Equal(500L * 1_000_000L, value.Frame.TimestampNs);
    }

    [Fact]
    public void Decide_anchor_uses_anchor_team_three_bar_for_goal_front_third()
    {
        BallDisappearedReplayStrategy testee = new(CreateSlantedTableConfig());

        Record(testee, 0, BarType.B3, TrackingConfidence.High, position: new Point(0, 350));
        Record(testee, 500, BarType.B3, TrackingConfidence.High, position: new Point(500, 350));
        Record(testee, 1000, BarType.B3, TrackingConfidence.High, position: new Point(1000, 350));

        Option<ReplayAnchor> anchor = DecideLost(testee, 3 * _1s);

        Assert.True(anchor.TryGetValue(out ReplayAnchor value));
        Assert.Equal(500L * 1_000_000L, value.Frame.TimestampNs);
    }

    [Fact]
    public void Decide_anchor_returns_none_without_high_speed_when_any_candidate_between_anchor_and_ball_loss_is_not_in_front_of_goal()
    {
        Record(0, BarType.A3, TrackingConfidence.High, position: InFrontOfGoalPosition(0));
        Record(500, BarType.A3, TrackingConfidence.High, position: InFrontOfGoalPosition(500));
        Record(1000, BarType.A3, TrackingConfidence.High, position: OutsideGoalFrontPosition(1000));

        Option<ReplayAnchor> anchor = DecideLost(3 * _1s);

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_anchor_ignores_high_speed_before_anchor_window()
    {
        Record(0, BarType.A3, TrackingConfidence.High, HighGoalSpeed());
        Record(500, BarType.A3, TrackingConfidence.High);
        Record(1000, BarType.A3, TrackingConfidence.High);

        Option<ReplayAnchor> anchor = DecideLost(3 * _1s);

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_anchor_ignores_candidates_outside_history_window()
    {
        Record(0, BarType.A3, TrackingConfidence.High);
        Record(500, BarType.A3, TrackingConfidence.High);
        Record(1000, BarType.A3, TrackingConfidence.High, HighGoalSpeed());

        Option<ReplayAnchor> anchor = DecideLost(7 * _1s);

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_waits_one_second_after_last_observed_frame()
    {
        Record(0, BarType.A3, TrackingConfidence.High);
        Record(500, BarType.A3, TrackingConfidence.High);
        Record(1000, BarType.A3, TrackingConfidence.High, HighGoalSpeed());

        Option<ReplayAnchor> earlyAnchor = DecideLost(1999L * 1_000_000L);
        Option<ReplayAnchor> dueAnchor = DecideLost(2000L * 1_000_000L);

        Assert.True(earlyAnchor.IsNone);
        Assert.True(dueAnchor.IsSome);
    }

    [Fact]
    public void Decide_evaluates_only_once_during_continuous_loss()
    {
        Record(0, BarType.A3, TrackingConfidence.High);
        Record(500, BarType.A3, TrackingConfidence.High);
        Record(1000, BarType.A3, TrackingConfidence.High, HighGoalSpeed());

        Option<ReplayAnchor> firstAnchor = DecideLost(2000L * 1_000_000L);
        Option<ReplayAnchor> secondAnchor = DecideLost(3000L * 1_000_000L);

        Assert.True(firstAnchor.IsSome);
        Assert.True(secondAnchor.IsNone);
    }

    private void Record(
        long timeMs,
        BarType bar,
        TrackingConfidence confidence,
        Vector2? velocity = null,
        Point? position = null)
    {
        Record(_Testee, timeMs, bar, confidence, velocity, position);
    }

    private static void Record(
        BallDisappearedReplayStrategy testee,
        long timeMs,
        BarType bar,
        TrackingConfidence confidence,
        Vector2? velocity = null,
        Point? position = null)
    {
        Point candidatePosition = position ?? Position(timeMs);
        _ = testee.Decide(
            new Frame((ulong)timeMs, timeMs * 1_000_000L),
            true,
            new ReplayCandidate(
                new Frame((ulong)timeMs, timeMs * 1_000_000L),
                candidatePosition,
                CreatePossession(bar),
                (int)timeMs,
                velocity ?? default,
                confidence,
                bar));
    }

    private Option<ReplayAnchor> DecideLost(long timestampNs)
        => DecideLost(_Testee, timestampNs);

    private static Option<ReplayAnchor> DecideLost(BallDisappearedReplayStrategy testee, long timestampNs)
        => testee.Decide(new Frame(999, timestampNs), false, null);

    private static Point Position(long timeMs)
        => new(timeMs, timeMs + 1);

    private static Point InFrontOfGoalPosition(long timeMs)
        => new(timeMs, 450);

    private static Point OutsideGoalFrontPosition(long timeMs)
        => new(timeMs, 650);

    private static Vector2 HighGoalSpeed()
        => new(4000, 0);

    private static BallPossession CreatePossession(BarType bar)
    {
        return bar switch
        {
            BarType.A1 or BarType.A2 => new BallPossession(Team.A, PossessionArea.Defense),
            BarType.B1 or BarType.B2 => new BallPossession(Team.B, PossessionArea.Defense),
            BarType.A5 => new BallPossession(Team.A, PossessionArea.FiveBar),
            BarType.B5 => new BallPossession(Team.B, PossessionArea.FiveBar),
            BarType.A3 => new BallPossession(Team.A, PossessionArea.ThreeBar),
            _ => new BallPossession(Team.B, PossessionArea.ThreeBar),
        };
    }

    private static TableConfiguration CreateSlantedTableConfig()
    {
        TableConfiguration config = TableConfig.Config;
        Trapezium boundary = config.Field.Boundary;

        PlayingField field = config.Field with
        {
            Boundary = new Trapezium(
                new Point(boundary.UpperLeft.X, 300),
                new Point(boundary.UpperRight.X, 0),
                new Point(boundary.LowerLeft.X, 600),
                new Point(boundary.LowerRight.X, 300)),
        };

        return config with { Field = field };
    }
}
