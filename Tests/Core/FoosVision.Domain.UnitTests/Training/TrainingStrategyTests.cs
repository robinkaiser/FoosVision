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
    [InlineData(1139, 450, false)]
    [InlineData(1140, 450, true)]
    [InlineData(1170, 450, true)]
    [InlineData(1171, 450, false)]
    [InlineData(1150, 400, false)]
    [InlineData(1150, 401, true)]
    [InlineData(1150, 499, true)]
    [InlineData(1150, 500, false)]
    public void Snake_region_uses_rod_centers_and_middle_fifteen_percent(double x, double y, bool ready)
    {
        SnakeShotTrainingStrategy strategy = Snake();
        strategy.FindPreparation(Snapshot(0, new(x, y)));
        Assert.Equal(ready, strategy.FindPreparation(Snapshot(1000, new(x, y))) is not null);
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
    public void Shot_requires_high_speed_and_target_two_bar_then_exact_tail(Team team, bool sideSpeed)
    {
        SnakeShotTrainingStrategy strategy = Snake();
        BeginShot(strategy, team);
        Vector2 velocity = sideSpeed ? new(0, 2000) : new(2000, 0);
        Point moving = SnakePosition(team) with { Y = 510 };
        Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(Snapshot(1100, moving, Three(team), velocity)).State);
        TrainingExecution confirmed = strategy.Evaluate(Snapshot(1200, new(team == Team.A ? 1300 : 300, 450), Target(team)));
        Assert.Equal(TrainingExecutionState.Pending, confirmed.State);
        Assert.Equal(At(1200), confirmed.ConfirmationFrame);
        Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(Snapshot(1449, default, phase: LiveTrackingPhase.BallTemporarilyLost)).State);
        Assert.Equal(TrainingExecutionState.Completed, strategy.Evaluate(Snapshot(1450, default, phase: LiveTrackingPhase.BallTemporarilyLost)).State);
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
    [InlineData(Team.A, true)]
    [InlineData(Team.A, false)]
    [InlineData(Team.B, true)]
    [InlineData(Team.B, false)]
    public void Pass_completes_on_own_three_or_opponent_defense(Team team, bool ownThree)
    {
        TickTackPassTrainingStrategy strategy = BeginPass(team);
        BarType target = ownThree ? Three(team) : Target(team);
        Point targetPosition = new(team == Team.A ? 1100 : 500, 200);
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
        Point exit = backwards ? new(x + (team == Team.A ? -20 : 20), 200) : new(x, 450);
        Assert.Equal(TrainingExecutionState.Pending, strategy.Evaluate(Snapshot(400, exit, Five(team))).State);
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
