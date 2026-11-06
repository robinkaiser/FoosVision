// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.Services;
using FoosVision.Domain.Training.ValueObjects;
using static FoosVision.Domain.UnitTests.Training.TrainingTestData;

namespace FoosVision.Domain.UnitTests.Training;

public class TrainingStrategyTests
{
    [Fact]
    public void Session_only_detects_enabled_strategies_and_announces_configured_variant_text()
    {
        TrainingOptions options = new([new(TrainingStrategyKind.PullShot, new(0, 0, WaitDistribution.Uniform), [new("long", "Lang", 1)])]);
        FoosVision.Domain.Training.Entities.TrainingSession session = Session(options);
        Point outsidePull = SnakePosition(Team.A) with { Y = 480 };
        session.ApplyTracking(Snapshot(0, outsidePull));
        Assert.Null(session.ApplyTracking(Snapshot(1000, outsidePull)).Attempt);
        session.ApplyTracking(Snapshot(2000, Pull(Team.A)));
        Assert.Equal(TrainingStrategyKind.PullShot, session.ApplyTracking(Snapshot(3000, Pull(Team.A))).Attempt!.Strategy);
        Assert.Equal(new TrainingVariant("long", "Lang", 1), session.ApplyTracking(Snapshot(3001, Pull(Team.A))).Variant);
    }

    [Theory]
    [InlineData(Team.A, false)]
    [InlineData(Team.B, false)]
    [InlineData(Team.A, true)]
    [InlineData(Team.B, true)]
    public void Snake_accepts_rest_or_movement_inside_region_after_exactly_one_second(Team team, bool moving)
    {
        SnakeShotTrainingStrategy strategy = Snake();
        Assert.Null(strategy.FindPreparation(Snapshot(0, SnakePosition(team), Three(team))));
        Point position = moving ? SnakePosition(team) with { Y = 480 } : SnakePosition(team);
        Assert.Null(strategy.FindPreparation(Snapshot(999, position, Three(team))));
        TrainingPreparation preparation = Assert.IsType<TrainingPreparation>(strategy.FindPreparation(Snapshot(1000, position, Three(team))));
        Assert.Equal(team, preparation.Team);
        Assert.Equal(Three(team), preparation.Bar);
        strategy.Begin(preparation);
        Assert.Equal(TrainingExecutionState.Holding, strategy.Evaluate(Snapshot(1100, SnakePosition(team), Three(team))).State);
    }

    [Theory]
    [InlineData(1109, 450, false)]
    [InlineData(1110, 450, true)]
    [InlineData(1160, 450, true)]
    [InlineData(1161, 450, false)]
    [InlineData(1150, 383, false)]
    [InlineData(1150, 384, true)]
    [InlineData(1150, 516, true)]
    [InlineData(1150, 517, false)]
    public void Snake_region_uses_rod_centers_and_middle_twenty_percent(double x, double y, bool ready)
    {
        foreach (Team team in new[] { Team.A, Team.B })
        {
            SnakeShotTrainingStrategy strategy = Snake();
            double sideX = team == Team.A ? x :
                TableConfig.Config.Field.Bars.A3.Center.P0.X + TableConfig.Config.Field.Bars.B3.Center.P0.X - x;
            Point position = new(sideX, y);
            strategy.FindPreparation(Snapshot(0, position, Three(team)));
            Assert.Equal(ready, strategy.FindPreparation(Snapshot(1000, position, Three(team))) is not null);
        }
    }

    [Fact]
    public void Leaving_Snake_region_before_ready_restarts_the_delay_and_wrong_row_never_prepares()
    {
        SnakeShotTrainingStrategy strategy = Snake();
        strategy.FindPreparation(Snapshot(0, SnakePosition(Team.A)));
        strategy.FindPreparation(Snapshot(500, new(1180, 450)));
        Assert.Null(strategy.FindPreparation(Snapshot(1000, SnakePosition(Team.A))));
        Assert.Null(strategy.FindPreparation(Snapshot(1999, SnakePosition(Team.A))));
        Assert.NotNull(strategy.FindPreparation(Snapshot(2000, SnakePosition(Team.A))));
        strategy.Reset();
        strategy.FindPreparation(Snapshot(3000, SnakePosition(Team.A), BarType.A5));
        Assert.Null(strategy.FindPreparation(Snapshot(4000, SnakePosition(Team.A), BarType.A5)));
    }

    [Theory]
    [InlineData(Team.A)]
    [InlineData(Team.B)]
    public void Pull_rest_is_mirrored_and_has_no_goal_axis_restriction(Team team)
    {
        PullShotTrainingStrategy strategy = PullStrategy();
        Point position = Pull(team) with { X = 800 };
        strategy.FindPreparation(Snapshot(0, position, Three(team)));
        Assert.Null(strategy.FindPreparation(Snapshot(999, position, Three(team))));
        TrainingPreparation preparation = Assert.IsType<TrainingPreparation>(strategy.FindPreparation(Snapshot(1000, position, Three(team))));
        strategy.Begin(preparation);
        Assert.Equal(TrainingExecutionState.Holding, strategy.Evaluate(Snapshot(1100, position with { X = 805, Y = position.Y + 5 }, Three(team))).State);
        Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(Snapshot(1200, position with { X = 806, Y = position.Y + 5 }, Three(team))).State);
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(6, false)]
    public void Pull_preparation_uses_squared_distance_fifty_from_mean(double jitter, bool ready)
    {
        PullShotTrainingStrategy strategy = PullStrategy();
        Point position = Pull(Team.A);
        strategy.FindPreparation(Snapshot(0, new(position.X - jitter, position.Y - jitter)));
        Assert.Equal(ready, strategy.FindPreparation(Snapshot(1000, new(position.X + jitter, position.Y + jitter))) is not null);
    }

    [Theory]
    [InlineData(0.249, false)]
    [InlineData(0.25, true)]
    [InlineData(0.50, true)]
    [InlineData(0.501, false)]
    public void Pull_region_includes_twenty_five_to_fifty_percent_and_mirrors_for_both_teams(double fraction, bool ready)
    {
        foreach (Team team in new[] { Team.A, Team.B })
        {
            PullShotTrainingStrategy strategy = PullStrategy();
            Trapezium boundary = TableConfig.Config.Field.Boundary;
            double side = team == Team.A ? fraction : 1 - fraction;
            Point position = Pull(team) with
            {
                Y = boundary.UpperLeft.Y + (side * (boundary.LowerLeft.Y - boundary.UpperLeft.Y)),
            };
            strategy.FindPreparation(Snapshot(0, position, Three(team)));

            Assert.Equal(ready, strategy.FindPreparation(Snapshot(1000, position, Three(team))) is not null);
        }
    }

    [Theory]
    [InlineData(Team.A)]
    [InlineData(Team.B)]
    public void Pull_movement_can_confirm_shot_instead_of_immediate_setup_abort(Team team)
    {
        PullShotTrainingStrategy strategy = PullStrategy();
        Point position = Pull(team);
        strategy.FindPreparation(Snapshot(0, position, Three(team)));
        strategy.Begin(strategy.FindPreparation(Snapshot(1000, position, Three(team)))!);
        Assert.Equal(TrainingExecutionState.Pending,
            strategy.Evaluate(Snapshot(1100, position with { Y = position.Y + 10 }, Three(team))).State);
        Assert.Equal(TrainingExecutionState.Pending,
            strategy.Evaluate(Snapshot(1150, position with { Y = position.Y + 30 }, Three(team), new(0, 2000))).State);
        Assert.Equal(TrainingExecutionState.Pending,
            strategy.Evaluate(Snapshot(1200, new(team == Team.A ? 1300 : 300, 450), Target(team))).State);
        Assert.Equal(TrainingExecutionState.Completed,
            strategy.Evaluate(Snapshot(1450, default, phase: LiveTrackingPhase.BallTemporarilyLost)).State);
    }

    [Theory]
    [InlineData(Team.A, false)]
    [InlineData(Team.A, true)]
    [InlineData(Team.B, false)]
    [InlineData(Team.B, true)]
    public void Tick_tack_needs_two_real_reversals_at_either_side_for_each_team(Team team, bool lower)
    {
        TickTackPassTrainingStrategy strategy = TickTack();
        double startY = lower ? 700 : 200;
        double x = team == Team.A ? 700 : 900;
        Assert.Null(strategy.FindPreparation(Snapshot(0, new(x, startY), Five(team))));
        Assert.Null(strategy.FindPreparation(Snapshot(100, new(x, startY + 10), Five(team))));
        Assert.Null(strategy.FindPreparation(Snapshot(200, new(x, startY), Five(team))));
        Assert.NotNull(strategy.FindPreparation(Snapshot(300, new(x, startY + 10), Five(team))));
    }

    [Theory]
    [InlineData(0.35, true)]
    [InlineData(0.3501, false)]
    [InlineData(0.6499, false)]
    [InlineData(0.65, true)]
    public void Pass_uses_outer_35_percent_for_preparation_and_holding_for_both_teams(double sideFraction, bool allowed)
    {
        foreach (Team team in new[] { Team.A, Team.B })
        {
            TickTackPassTrainingStrategy strategy = TickTack();
            double x = team == Team.A ? 700 : 900;
            double y = 120 + 660 * sideFraction;
            double otherY = y + (sideFraction < 0.5 ? -10 : 10);
            BarType bar = Five(team);
            strategy.FindPreparation(Snapshot(0, new(x, otherY), bar));
            strategy.FindPreparation(Snapshot(100, new(x, y), bar));
            strategy.FindPreparation(Snapshot(200, new(x, otherY), bar));
            TrainingPreparation? preparation = strategy.FindPreparation(Snapshot(300, new(x, y), bar));
            Assert.Equal(allowed, preparation is not null);
            if (preparation is null)
            {
                continue;
            }

            strategy.Begin(preparation);
            Assert.Equal(TrainingExecutionState.Holding, strategy.Evaluate(Snapshot(400, new(x, y), bar)).State);
        }
    }

    [Fact]
    public void Tick_tack_does_not_count_jitter_or_combine_different_sides_or_teams()
    {
        TickTackPassTrainingStrategy strategy = TickTack();
        for (int i = 0; i < 20; i++)
        {
            Assert.Null(strategy.FindPreparation(Snapshot(i * 10, new(700, 200 + (i % 2)), BarType.A5)));
        }

        strategy.FindPreparation(Snapshot(300, new(700, 220), BarType.A5));
        strategy.FindPreparation(Snapshot(400, new(700, 200), BarType.A5));
        Assert.Null(strategy.FindPreparation(Snapshot(500, new(700, 700), BarType.A5)));
        Assert.Null(strategy.FindPreparation(Snapshot(600, new(900, 200), BarType.B5)));
        Assert.Null(strategy.FindPreparation(Snapshot(700, new(1100, 210), BarType.A3)));
    }

    [Theory]
    [InlineData(Team.A, true)]
    [InlineData(Team.A, false)]
    [InlineData(Team.B, true)]
    [InlineData(Team.B, false)]
    public void Shot_requires_high_speed_and_opponent_defense_then_exact_tail(Team team, bool sideSpeed)
    {
        SnakeShotTrainingStrategy strategy = Snake();
        BeginShot(strategy, team);
        Vector2 velocity = sideSpeed ? new(0, 2000) : new(2000, 0);
        Point moving = SnakePosition(team) with { Y = 530 };
        Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(Snapshot(1100, moving, Three(team), velocity)).State);
        TrainingExecution confirmed = strategy.Evaluate(Snapshot(1200, new(team == Team.A ? 1300 : 300, 450), Target(team)));
        Assert.Equal(TrainingExecutionState.Pending, confirmed.State);
        Assert.Equal(At(1200), confirmed.ConfirmationFrame);
        Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(Snapshot(1449, default, phase: LiveTrackingPhase.BallTemporarilyLost)).State);
        Assert.Equal(TrainingExecutionState.Completed, strategy.Evaluate(Snapshot(1450, default, phase: LiveTrackingPhase.BallTemporarilyLost)).State);
    }

    [Theory]
    [InlineData(TrainingStrategyKind.SnakeShot, Team.A, BarType.B1)]
    [InlineData(TrainingStrategyKind.SnakeShot, Team.A, BarType.B2)]
    [InlineData(TrainingStrategyKind.SnakeShot, Team.B, BarType.A1)]
    [InlineData(TrainingStrategyKind.SnakeShot, Team.B, BarType.A2)]
    [InlineData(TrainingStrategyKind.PullShot, Team.A, BarType.B1)]
    [InlineData(TrainingStrategyKind.PullShot, Team.A, BarType.B2)]
    [InlineData(TrainingStrategyKind.PullShot, Team.B, BarType.A1)]
    [InlineData(TrainingStrategyKind.PullShot, Team.B, BarType.A2)]
    public void Both_shots_accept_either_opponent_defense_bar_in_either_order(
        TrainingStrategyKind kind, Team team, BarType defenseBar)
    {
        foreach (bool speedFirst in new[] { false, true })
        {
            ITrainingStrategy strategy = kind == TrainingStrategyKind.SnakeShot ? Snake() : PullStrategy();
            Point setup = kind == TrainingStrategyKind.SnakeShot ? SnakePosition(team) : Pull(team);
            strategy.FindPreparation(Snapshot(0, setup, Three(team)));
            strategy.Begin(strategy.FindPreparation(Snapshot(1000, setup, Three(team)))!);
            Bar bar = TableConfig.Config.Field.Bars[defenseBar];
            Point target = new((bar.Center.P0.X + bar.Center.P1.X) / 2, 450);
            LiveTrackingSnapshot speed = Snapshot(speedFirst ? 1100 : 1200, setup with { Y = setup.Y + 100 }, Three(team), new(2000, 0));
            LiveTrackingSnapshot detection = Snapshot(speedFirst ? 1200 : 1100, target, defenseBar);

            Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(speedFirst ? speed : detection).State);
            TrainingExecution confirmed = strategy.Evaluate(speedFirst ? detection : speed);

            Assert.Equal(At(1200), confirmed.ConfirmationFrame);
            Assert.Equal(TrainingExecutionState.Pending, confirmed.State);
            Assert.Equal(TrainingExecutionState.Pending,
                strategy.Evaluate(Snapshot(1449, default, phase: LiveTrackingPhase.BallTemporarilyLost)).State);
            Assert.Equal(TrainingExecutionState.Completed,
                strategy.Evaluate(Snapshot(1450, default, phase: LiveTrackingPhase.BallTemporarilyLost)).State);
        }
    }

    [Theory]
    [InlineData(TrainingStrategyKind.SnakeShot, Team.A, BarType.A1)]
    [InlineData(TrainingStrategyKind.SnakeShot, Team.A, BarType.A2)]
    [InlineData(TrainingStrategyKind.SnakeShot, Team.B, BarType.B1)]
    [InlineData(TrainingStrategyKind.SnakeShot, Team.B, BarType.B2)]
    [InlineData(TrainingStrategyKind.PullShot, Team.A, BarType.A1)]
    [InlineData(TrainingStrategyKind.PullShot, Team.A, BarType.A2)]
    [InlineData(TrainingStrategyKind.PullShot, Team.B, BarType.B1)]
    [InlineData(TrainingStrategyKind.PullShot, Team.B, BarType.B2)]
    public void Own_defense_does_not_confirm_a_shot(TrainingStrategyKind kind, Team team, BarType ownDefense)
    {
        ITrainingStrategy strategy = kind == TrainingStrategyKind.SnakeShot ? Snake() : PullStrategy();
        Point setup = kind == TrainingStrategyKind.SnakeShot ? SnakePosition(team) : Pull(team);
        strategy.FindPreparation(Snapshot(0, setup, Three(team)));
        strategy.Begin(strategy.FindPreparation(Snapshot(1000, setup, Three(team)))!);
        Bar bar = TableConfig.Config.Field.Bars[ownDefense];
        Point defensePosition = new((bar.Center.P0.X + bar.Center.P1.X) / 2, 450);
        strategy.Evaluate(Snapshot(1100, setup with { Y = setup.Y + 100 }, Three(team), new(2000, 0)));
        strategy.Evaluate(Snapshot(1200, defensePosition, ownDefense));

        TrainingExecution result = strategy.Evaluate(Snapshot(1850, defensePosition, ownDefense));

        Assert.Equal(TrainingExecutionState.Lost, result.State);
        Assert.Null(result.ConfirmationFrame);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Shot_missing_speed_or_target_is_aborted_only_after_decision_window(bool highSpeed, bool target)
    {
        SnakeShotTrainingStrategy strategy = Snake();
        BeginShot(strategy, Team.A);
        strategy.Evaluate(Snapshot(1100, new(1200, 450), velocity: highSpeed ? new(2000, 0) : default));
        BarType bar = target ? BarType.B2 : BarType.B3;
        Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(Snapshot(1849, new(1300, 450), bar)).State);
        Assert.Equal(TrainingExecutionState.Lost, strategy.Evaluate(Snapshot(1850, new(1300, 450), bar)).State);
    }

    [Theory]
    [InlineData(false, 4, true)]
    [InlineData(true, 4, true)]
    [InlineData(false, 3.99, false)]
    [InlineData(true, 3.99, false)]
    public void Both_speed_axes_use_four_kmh_threshold(bool side, double speedKmh, bool complete)
    {
        SnakeShotTrainingStrategy strategy = Snake();
        BeginShot(strategy, Team.A);
        TableImageScale scale = TableImageScale.From(TableConfig.Config);
        double pxPerSecond = side
            ? speedKmh / scale.ConvertSideAxisSpeedPxPerSToKmh(1)
            : speedKmh / scale.ConvertGoalAxisSpeedPxPerSToKmh(1);
        Vector2 velocity = side ? new(0, pxPerSecond) : new(pxPerSecond, 0);
        strategy.Evaluate(Snapshot(1100, new(1200, 450), velocity: velocity));
        strategy.Evaluate(Snapshot(1200, new(1300, 450), BarType.B2));
        Assert.Equal(complete ? TrainingExecutionState.Completed : TrainingExecutionState.Lost,
            strategy.Evaluate(Snapshot(1850, new(1300, 450), BarType.B2)).State);
    }

    [Theory]
    [InlineData(1850, TrainingExecutionState.Pending)]
    [InlineData(1851, TrainingExecutionState.Lost)]
    public void Target_detection_must_arrive_within_decision_window(long targetMs, TrainingExecutionState expected)
    {
        SnakeShotTrainingStrategy strategy = Snake();
        BeginShot(strategy, Team.A);
        strategy.Evaluate(Snapshot(1100, new(1200, 450), velocity: new(2000, 0)));
        Assert.Equal(expected, strategy.Evaluate(Snapshot(targetMs, new(1300, 450), BarType.B2)).State);
    }

    [Fact]
    public void Snake_side_region_is_calculated_at_three_bar_in_skewed_field()
    {
        Trapezium boundary = TableConfig.Config.Field.Boundary;
        TableConfiguration skewed = TableConfig.Config with
        {
            Field = TableConfig.Config.Field with
            {
                Boundary = boundary with
                {
                    UpperRight = boundary.UpperRight with { Y = 300 },
                    LowerRight = boundary.LowerRight with { Y = 960 },
                },
            },
        };
        SnakeShotTrainingStrategy strategy = new(Option(TrainingStrategyKind.SnakeShot), skewed);
        double centerYAtThree = 450 + (180 * (1100.0 - 120) / (1480 - 120));
        Point position = new(1150, centerYAtThree);
        strategy.FindPreparation(Snapshot(0, position));
        Assert.NotNull(strategy.FindPreparation(Snapshot(1000, position)));
        strategy.Reset();
        strategy.FindPreparation(Snapshot(2000, SnakePosition(Team.A)));
        Assert.Null(strategy.FindPreparation(Snapshot(3000, SnakePosition(Team.A))));
    }

    [Theory]
    [InlineData(Team.A, BarType.A3)]
    [InlineData(Team.A, BarType.B1)]
    [InlineData(Team.A, BarType.B2)]
    [InlineData(Team.B, BarType.B3)]
    [InlineData(Team.B, BarType.A1)]
    [InlineData(Team.B, BarType.A2)]
    public void Pass_completes_on_own_three_or_opponent_defense(Team team, BarType target)
    {
        TickTackPassTrainingStrategy strategy = BeginPass(team);
        Bar bar = TableConfig.Config.Field.Bars[target];
        Point targetPosition = new((bar.Center.P0.X + bar.Center.P1.X) / 2, 200);
        Assert.Equal(TrainingExecutionState.Completed, strategy.Evaluate(Snapshot(400, targetPosition, target)).State);
    }

    [Theory]
    [InlineData(Team.A, false)]
    [InlineData(Team.A, true)]
    [InlineData(Team.B, false)]
    [InlineData(Team.B, true)]
    public void Pass_sideways_exit_or_wrong_direction_cannot_become_success(Team team, bool backwards)
    {
        TickTackPassTrainingStrategy strategy = BeginPass(team);
        double x = team == Team.A ? 700 : 900;
        Point exit = backwards ? new(team == Team.A ? 500 : 1100, 200) : new(x, 450);
        BarType exitBar = backwards ? Three(team == Team.A ? Team.B : Team.A) : Five(team);
        Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(Snapshot(400, exit, exitBar)).State);
        Point target = new(team == Team.A ? 1100 : 500, 200);
        Assert.Equal(TrainingExecutionState.Lost, strategy.Evaluate(Snapshot(1150, target, Three(team))).State);
    }

    private static SnakeShotTrainingStrategy Snake() => new(Option(TrainingStrategyKind.SnakeShot), TableConfig.Config);
    private static PullShotTrainingStrategy PullStrategy() => new(Option(TrainingStrategyKind.PullShot), TableConfig.Config);
    private static TickTackPassTrainingStrategy TickTack() => new(Option(TrainingStrategyKind.TickTackPass), TableConfig.Config);

    private static void BeginShot(ITrainingStrategy strategy, Team team)
    {
        strategy.FindPreparation(Snapshot(0, SnakePosition(team), Three(team)));
        strategy.Begin(strategy.FindPreparation(Snapshot(1000, SnakePosition(team), Three(team)))!);
    }

    private static TickTackPassTrainingStrategy BeginPass(Team team)
    {
        TickTackPassTrainingStrategy strategy = TickTack();
        double x = team == Team.A ? 700 : 900;
        strategy.FindPreparation(Snapshot(0, new(x, 200), Five(team)));
        strategy.FindPreparation(Snapshot(100, new(x, 220), Five(team)));
        strategy.FindPreparation(Snapshot(200, new(x, 200), Five(team)));
        strategy.Begin(strategy.FindPreparation(Snapshot(300, new(x, 220), Five(team)))!);
        return strategy;
    }
}
