// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision.Strategies;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.TrackingCore.ReplayDecision.Strategies;

public class SavedShotReplayStrategyTests
{
    private readonly SavedShotReplayStrategy _Testee = new(TableImageScale.From(TableConfig.Config));

    [Fact]
    public void Decide_triggers_when_three_bar_shot_is_saved_on_defense()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.B5, HighGoalSpeed());

        Option<ReplayAnchor> anchor = Record(4050, BarType.B2, LowSpeed());

        Assert.True(anchor.TryGetValue(out ReplayAnchor value));
        Assert.Equal(2000L * 1_000_000L, value.Frame.TimestampNs);
        Assert.Equal(Position(2000), value.Position);
        Assert.Equal(ReplayTriggerKind.SavedShot, value.TriggerKind);
    }

    [Fact]
    public void Decide_triggers_when_three_bar_shot_returns_to_three_bar()
    {
        RecordThreeBarPossession(lastVelocity: HighSideSpeed());
        Record(3050, BarType.B5, LowSpeed());

        Option<ReplayAnchor> anchor = Record(4050, BarType.A3, LowSpeed());

        Assert.True(anchor.IsSome);
    }

    [Fact]
    public void Decide_triggers_when_high_speed_is_detected_inside_after_exit_window()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.B5, LowSpeed());
        Record(3100, BarType.B5, HighGoalSpeed());

        Option<ReplayAnchor> anchor = Record(4050, BarType.B1, LowSpeed());

        Assert.True(anchor.IsSome);
    }

    [Fact]
    public void Decide_keeps_three_bar_possession_while_ball_is_not_observed()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.A3, HighGoalSpeed(), isBallObserved: false, confidence: TrackingConfidence.Low);
        Record(3250, BarType.B5, LowSpeed(), isBallObserved: false);

        Option<ReplayAnchor> anchor = Record(4250, BarType.B1, LowSpeed());

        Assert.True(anchor.IsSome);
    }

    [Fact]
    public void Decide_does_not_treat_missing_candidate_as_three_bar_exit()
    {
        RecordThreeBarPossession(lastVelocity: HighGoalSpeed());
        RecordLost(3050);

        Option<ReplayAnchor> anchor = Record(4050, BarType.A3, LowSpeed());

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_triggers_when_high_speed_is_detected_after_three_bar_possession_qualified()
    {
        Record(0, BarType.A3, LowSpeed());
        Record(1000, BarType.A3, LowSpeed());
        Record(2000, BarType.A3, LowSpeed());
        Record(3000, BarType.A3, LowSpeed());
        Record(3200, BarType.A3, HighGoalSpeed());
        Record(3250, BarType.B5, LowSpeed());

        Option<ReplayAnchor> anchor = Record(4250, BarType.B1, LowSpeed());

        Assert.True(anchor.IsSome);
    }

    [Fact]
    public void Decide_ignores_high_speed_before_three_bar_possession_qualified()
    {
        Record(0, BarType.A3, LowSpeed());
        Record(1000, BarType.A3, LowSpeed());
        Record(2000, BarType.A3, LowSpeed());
        Record(2800, BarType.A3, HighGoalSpeed());
        Record(3000, BarType.A3, LowSpeed());
        Record(3050, BarType.B5, LowSpeed());

        Option<ReplayAnchor> anchor = Record(4551, BarType.B1, LowSpeed());

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_ignores_high_speed_after_exit_window()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.B5, LowSpeed());
        Record(3560, BarType.B5, HighGoalSpeed());

        Option<ReplayAnchor> anchor = Record(4551, BarType.B1, LowSpeed());

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_requires_three_seconds_on_three_bar()
    {
        Record(0, BarType.A3, LowSpeed());
        Record(1000, BarType.A3, LowSpeed());
        Record(2000, BarType.A3, LowSpeed());
        Record(2050, BarType.B5, HighGoalSpeed());

        Option<ReplayAnchor> anchor = Record(4050, BarType.B1, LowSpeed());

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_waits_until_decision_window_after_leaving_three_bar()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.B5, HighGoalSpeed());

        Option<ReplayAnchor> anchor = Record(4049, BarType.B1, LowSpeed());

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_triggers_when_ball_is_observed_later_inside_decision_window()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.B5, HighGoalSpeed());
        Record(4050, BarType.B1, LowSpeed(), isBallObserved: false);

        Option<ReplayAnchor> anchor = Record(4300, BarType.B1, LowSpeed());

        Assert.True(anchor.IsSome);
    }

    [Fact]
    public void Decide_rejects_when_ball_is_not_on_three_bar_or_defense_before_window_expires()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.B5, HighGoalSpeed());
        Record(4050, BarType.B5, LowSpeed());

        Option<ReplayAnchor> anchor = Record(4551, BarType.B5, LowSpeed());

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_rejects_when_ball_is_not_observed_before_window_expires()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.B5, HighGoalSpeed());
        Record(4050, BarType.B1, LowSpeed(), isBallObserved: false);

        Option<ReplayAnchor> anchor = Record(4551, BarType.B1, LowSpeed(), isBallObserved: false);

        Assert.True(anchor.IsNone);
    }

    [Fact]
    public void Decide_rejects_when_no_high_speed_was_detected()
    {
        RecordThreeBarPossession();
        Record(3050, BarType.B5, LowSpeed());

        Option<ReplayAnchor> anchor = Record(4551, BarType.B1, LowSpeed());

        Assert.True(anchor.IsNone);
    }

    private void RecordThreeBarPossession(Vector2? lastVelocity = null)
    {
        Record(0, BarType.A3, LowSpeed());
        Record(1000, BarType.A3, LowSpeed());
        Record(2000, BarType.A3, LowSpeed());
        Record(3000, BarType.A3, lastVelocity ?? LowSpeed());
    }

    private Option<ReplayAnchor> Record(
        long timeMs,
        BarType bar,
        Vector2 velocity,
        bool isBallObserved = true,
        TrackingConfidence confidence = TrackingConfidence.High)
    {
        return _Testee.Decide(
            new Frame((ulong)timeMs, timeMs * 1_000_000L),
            isBallObserved,
            new ReplayCandidate(
                new Frame((ulong)timeMs, timeMs * 1_000_000L),
                Position(timeMs),
                CreatePossession(bar),
                (int)timeMs,
                velocity,
                confidence,
                bar));
    }

    private Option<ReplayAnchor> RecordLost(long timeMs)
        => _Testee.Decide(
            new Frame((ulong)timeMs, timeMs * 1_000_000L),
            false,
            null);

    private static Point Position(long timeMs)
        => new(timeMs, timeMs + 1);

    private static Vector2 LowSpeed()
        => new(0, 0);

    private static Vector2 HighGoalSpeed()
        => new(4000, 0);

    private static Vector2 HighSideSpeed()
        => new(0, 1200);

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
}
