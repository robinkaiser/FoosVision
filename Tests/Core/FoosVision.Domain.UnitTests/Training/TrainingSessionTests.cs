// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.ValueObjects;
using static FoosVision.Domain.UnitTests.Training.TrainingTestData;

namespace FoosVision.Domain.UnitTests.Training;

public class TrainingSessionTests
{
    [Fact]
    public void Ready_starts_strategy_wait_once_and_variant_needs_no_audio_feedback()
    {
        CountingRandom random = new(0.5, 0.99);
        TrainingSession session = Session(new([Option(TrainingStrategyKind.SnakeShot, 1, 3)]), random);
        Assert.Empty(session.ApplyTracking(Snapshot(0, SnakePosition(Team.A))).Events);
        TrainingFrameResult ready = session.ApplyTracking(Snapshot(1000, SnakePosition(Team.A)));
        TrainingPreparationConfirmed confirmed = Assert.Single(ready.Events.OfType<TrainingPreparationConfirmed>());
        TrainingAnnouncementRequested announcement = Assert.Single(ready.Events.OfType<TrainingAnnouncementRequested>());
        Assert.Equal(session.Id, confirmed.Attempt.SessionId);
        Assert.Equal(1, confirmed.Attempt.AttemptId);
        Assert.Equal(TrainingAnnouncementKind.Ready, announcement.Kind);
        Assert.Equal(1, random.Calls);
        Assert.Empty(session.ApplyTracking(Snapshot(2999, SnakePosition(Team.A))).Events);
        TrainingAnnouncementRequested variant = Assert.Single(session.ApplyTracking(Snapshot(3000, SnakePosition(Team.A))).Events.OfType<TrainingAnnouncementRequested>());
        Assert.Equal(TrainingAnnouncementKind.Variant, variant.Kind);
        Assert.Equal("middle", variant.Variant!.Id);
        Assert.True(variant.AnnouncementId > announcement.AnnouncementId);
        Assert.Equal(2, random.Calls);
        Assert.Empty(session.ApplyTracking(Snapshot(3100, SnakePosition(Team.A))).Events);
    }

    [Theory]
    [InlineData(Team.A)]
    [InlineData(Team.B)]
    public void Shot_completion_is_once_with_last_setup_anchor_and_next_attempt_needs_fresh_preparation(Team team)
    {
        TrainingSession session = Session();
        ReadyAndAnnounce(session, team);
        session.ApplyTracking(Snapshot(2100, SnakePosition(team) with { Y = 510 }, Three(team), new(2000, 0)));
        session.ApplyTracking(Snapshot(2200, new(team == Team.A ? 1300 : 300, 450), Target(team)));
        TrainingAttemptCompleted completed = Assert.Single(session.ApplyTracking(Snapshot(2450, default, Target(team))).Events.OfType<TrainingAttemptCompleted>());
        Assert.Equal(At(2000), completed.Attempt.Anchor.Frame);
        Assert.Equal(SnakePosition(team), completed.Attempt.Anchor.Position);
        Assert.Equal(At(2200), completed.ConfirmationFrame);
        Assert.Equal(At(2450), completed.CompletionFrame);
        Assert.Equal(TrainingState.Completed, session.State);
        Assert.Empty(session.ApplyTracking(Snapshot(2500, new(1300, 450), Target(team))).Events);
        Assert.Empty(session.ApplyTracking(Snapshot(2600, SnakePosition(team), Three(team))).Events);
        TrainingFrameResult next = session.ApplyTracking(Snapshot(3600, SnakePosition(team), Three(team)));
        TrainingPreparationConfirmed ready = Assert.Single(next.Events.OfType<TrainingPreparationConfirmed>());
        Assert.Equal(2, ready.Attempt.AttemptId);
        Assert.Equal(session.Id, ready.Attempt.SessionId);
        Assert.Single(next.Events.OfType<TrainingAnnouncementsInvalidated>());
    }

    [Fact]
    public void Premature_shot_invalidates_old_announcements_and_keeps_its_own_abort_valid()
    {
        TrainingSession session = Session(new([Option(TrainingStrategyKind.SnakeShot, 10, 10)]));
        session.ApplyTracking(Snapshot(0, SnakePosition(Team.A)));
        session.ApplyTracking(Snapshot(1000, SnakePosition(Team.A)));
        session.ApplyTracking(Snapshot(1100, new(1200, 450), velocity: new(2000, 0)));
        session.ApplyTracking(Snapshot(1200, new(1300, 450), BarType.B2));
        TrainingFrameResult result = session.ApplyTracking(Snapshot(1450, new(1300, 450), BarType.B2));
        Assert.Equal(TrainingAbortReason.PrematureExecution, Assert.Single(result.Events.OfType<TrainingAttemptAborted>()).Reason);
        TrainingAnnouncementsInvalidated invalidated = Assert.Single(result.Events.OfType<TrainingAnnouncementsInvalidated>());
        TrainingAnnouncementRequested aborted = Assert.Single(result.Events.OfType<TrainingAnnouncementRequested>());
        Assert.Equal(TrainingAnnouncementKind.Aborted, aborted.Kind);
        Assert.True(aborted.AnnouncementId > invalidated.ThroughAnnouncementId);
        Assert.DoesNotContain(result.Events, item => item is TrainingAttemptCompleted);
        Assert.Empty(session.ApplyTracking(Snapshot(11000, new(1300, 450), BarType.B2)).Events);
    }

    [Theory]
    [InlineData(TrainingStrategyKind.SnakeShot, false)]
    [InlineData(TrainingStrategyKind.SnakeShot, true)]
    [InlineData(TrainingStrategyKind.PullShot, false)]
    [InlineData(TrainingStrategyKind.PullShot, true)]
    public void Slow_setup_loss_aborts_once_before_or_after_variant(TrainingStrategyKind kind, bool afterVariant)
    {
        TrainingSession session = Session(new([Option(kind)]));
        Point setup = kind == TrainingStrategyKind.SnakeShot ? SnakePosition(Team.A) : Pull(Team.A);
        session.ApplyTracking(Snapshot(0, setup));
        session.ApplyTracking(Snapshot(1000, setup));
        if (afterVariant)
        {
            session.ApplyTracking(Snapshot(2000, setup));
        }

        long moveMs = afterVariant ? 2100 : 1100;
        Point moved = kind == TrainingStrategyKind.SnakeShot ? new(1180, 450) : setup with { X = setup.X + 8 };
        Assert.Empty(session.ApplyTracking(Snapshot(moveMs, moved)).Events);
        Assert.Equal(TrainingState.Evaluating, session.State);
        Assert.Empty(session.ApplyTracking(Snapshot(moveMs + 749, moved)).Events);
        TrainingFrameResult aborted = session.ApplyTracking(Snapshot(moveMs + 750, moved));
        Assert.Equal(TrainingAbortReason.PreparationLost, Assert.Single(aborted.Events.OfType<TrainingAttemptAborted>()).Reason);
        Assert.Single(aborted.Events.OfType<TrainingAnnouncementRequested>());
        Assert.Empty(session.ApplyTracking(Snapshot(moveMs + 751, moved)).Events);
    }

    [Fact]
    public void Three_second_timeout_does_not_repeat_ready_for_unchanged_setup()
    {
        TrainingSession session = Session();
        ReadyAndAnnounce(session, Team.A);
        Assert.Empty(session.ApplyTracking(Snapshot(4999, SnakePosition(Team.A))).Events);
        TrainingFrameResult result = session.ApplyTracking(Snapshot(5000, SnakePosition(Team.A)));
        Assert.Equal(TrainingAbortReason.ExecutionTimeout, Assert.Single(result.Events.OfType<TrainingAttemptAborted>()).Reason);
        Assert.Empty(session.ApplyTracking(Snapshot(6000, SnakePosition(Team.A))).Events);
        Assert.Empty(session.ApplyTracking(Snapshot(7000, SnakePosition(Team.A))).Events);
        session.ApplyTracking(Snapshot(7100, new(1200, 450)));
        session.ApplyTracking(Snapshot(7200, SnakePosition(Team.A)));
        Assert.Single(session.ApplyTracking(Snapshot(8200, SnakePosition(Team.A))).Events.OfType<TrainingPreparationConfirmed>());
    }

    [Fact]
    public void Execution_started_before_timeout_can_complete_after_timeout()
    {
        TrainingSession session = Session();
        ReadyAndAnnounce(session, Team.A);
        session.ApplyTracking(Snapshot(4999, new(1200, 450), velocity: new(2000, 0)));
        session.ApplyTracking(Snapshot(5100, new(1300, 450), BarType.B2));
        Assert.Single(session.ApplyTracking(Snapshot(5350, new(1300, 450), BarType.B2)).Events.OfType<TrainingAttemptCompleted>());
    }

    [Fact]
    public void Tracking_loss_duplicate_frames_and_stop_do_not_emit_stale_variants()
    {
        TrainingSession session = Session();
        session.ApplyTracking(Snapshot(0, SnakePosition(Team.A)));
        session.ApplyTracking(Snapshot(1000, SnakePosition(Team.A)));
        Assert.Empty(session.ApplyTracking(Snapshot(2000, default, phase: LiveTrackingPhase.BallTemporarilyLost)).Events);
        Assert.Empty(session.ApplyTracking(Snapshot(1500, SnakePosition(Team.A))).Events);
        TrainingFrameResult lost = session.ApplyTracking(Snapshot(3000, default, phase: LiveTrackingPhase.BallLost));
        Assert.Equal(TrainingAbortReason.BallLost, Assert.Single(lost.Events.OfType<TrainingAttemptAborted>()).Reason);
        Assert.Empty(session.ApplyTracking(Snapshot(3000, SnakePosition(Team.A))).Events);
        Assert.Single(session.Stop().OfType<TrainingAnnouncementsInvalidated>());
        Assert.Empty(session.Stop());
        Assert.Empty(session.ApplyTracking(Snapshot(10000, SnakePosition(Team.A))).Events);
        Assert.Equal(TrainingState.Stopped, session.State);
    }

    [Theory]
    [InlineData(TrainingStrategyKind.PullShot)]
    [InlineData(TrainingStrategyKind.TickTackPass)]
    public void Multiple_strategies_switch_after_abort_with_their_own_waits_and_fresh_history(TrainingStrategyKind other)
    {
        CountingRandom random = new();
        TrainingSession session = Session(new([Option(TrainingStrategyKind.SnakeShot, 1, 1), Option(other, 2, 2)]), random);
        ReadyAndAnnounce(session, Team.A);
        session.ApplyTracking(Snapshot(5000, SnakePosition(Team.A)));
        long readyMs;
        if (other == TrainingStrategyKind.PullShot)
        {
            session.ApplyTracking(Snapshot(5100, Pull(Team.A)));
            session.ApplyTracking(Snapshot(6100, Pull(Team.A)));
            readyMs = 6100;
        }
        else
        {
            session.ApplyTracking(Snapshot(5100, new(700, 200), BarType.A5));
            session.ApplyTracking(Snapshot(5200, new(700, 220), BarType.A5));
            session.ApplyTracking(Snapshot(5300, new(700, 200), BarType.A5));
            session.ApplyTracking(Snapshot(5400, new(700, 220), BarType.A5));
            readyMs = 5400;
        }

        Assert.Equal(other, session.Attempt!.Strategy);
        Assert.Equal(2, session.Attempt.AttemptId);
        Point setup = other == TrainingStrategyKind.PullShot ? Pull(Team.A) : new(700, 220);
        BarType bar = other == TrainingStrategyKind.PullShot ? BarType.A3 : BarType.A5;
        Assert.Empty(session.ApplyTracking(Snapshot(readyMs + 1999, setup, bar)).Events);
        TrainingAnnouncementRequested variant = Assert.Single(session.ApplyTracking(Snapshot(readyMs + 2000, setup, bar)).Events.OfType<TrainingAnnouncementRequested>());
        Assert.Equal(other == TrainingStrategyKind.PullShot ? "short" : "wall", variant.Variant!.Id);
        Assert.Equal(4, random.Calls);
    }

    [Fact]
    public void Inactive_strategies_are_not_evaluated_and_calibration_does_not_restart_attempt()
    {
        TrainingSession session = Session();
        session.ApplyTracking(Snapshot(0, Pull(Team.A)));
        Assert.Empty(session.ApplyTracking(Snapshot(1000, Pull(Team.A))).Events);
        session.ApplyTracking(Snapshot(2000, SnakePosition(Team.A)));
        session.ApplyTracking(Snapshot(3000, SnakePosition(Team.A)));
        TrainingAttempt attempt = session.Attempt!;
        Bar bar = TableConfig.Config.Field.Bars.A3;
        TableConfiguration updated = TableConfig.Config with
        {
            Field = TableConfig.Config.Field with
            {
                Bars = TableConfig.Config.Field.Bars with
                {
                    A3 = bar with { Center = new(bar.Center.P0 with { X = bar.Center.P0.X + 1 }, bar.Center.P1 with { X = bar.Center.P1.X + 1 }) },
                },
            },
        };
        session.UpdateTableConfig(updated);
        Assert.Same(updated, session.TableConfig);
        Assert.Same(attempt, session.Attempt);
        Assert.Equal(TrainingState.Waiting, session.State);
        Assert.Equal(TrainingAnnouncementKind.Variant,
            Assert.Single(session.ApplyTracking(Snapshot(4000, SnakePosition(Team.A))).Events.OfType<TrainingAnnouncementRequested>()).Kind);
    }

    [Fact]
    public void Completed_shot_allows_direct_reentry_to_setup_without_extra_outside_frame()
    {
        TrainingSession session = Session();
        ReadyAndAnnounce(session, Team.A);
        session.ApplyTracking(Snapshot(2100, new(1200, 450), velocity: new(2000, 0)));
        session.ApplyTracking(Snapshot(2200, new(1300, 450), BarType.B2));
        session.ApplyTracking(Snapshot(2450, new(1300, 450), BarType.B2));
        session.ApplyTracking(Snapshot(2500, SnakePosition(Team.A)));
        Assert.Single(session.ApplyTracking(Snapshot(3500, SnakePosition(Team.A))).Events.OfType<TrainingPreparationConfirmed>());
    }

    [Theory]
    [InlineData(TrainingStrategyKind.SnakeShot, 0.0, "push")]
    [InlineData(TrainingStrategyKind.SnakeShot, 0.5, "pull")]
    [InlineData(TrainingStrategyKind.SnakeShot, 0.99, "middle")]
    [InlineData(TrainingStrategyKind.PullShot, 0.0, "short")]
    [InlineData(TrainingStrategyKind.PullShot, 0.5, "middle")]
    [InlineData(TrainingStrategyKind.PullShot, 0.99, "long")]
    [InlineData(TrainingStrategyKind.TickTackPass, 0.0, "wall")]
    [InlineData(TrainingStrategyKind.TickTackPass, 0.5, "lane")]
    [InlineData(TrainingStrategyKind.TickTackPass, 0.99, "split")]
    public void Each_strategy_selects_its_own_variants_uniformly(TrainingStrategyKind kind, double unit, string expected)
    {
        CountingRandom random = new(0, unit);
        TrainingSession session = Session(new([Option(kind)]), random);
        Point setup;
        BarType bar;
        long readyMs;
        if (kind == TrainingStrategyKind.TickTackPass)
        {
            bar = BarType.A5;
            setup = new(700, 220);
            session.ApplyTracking(Snapshot(0, new(700, 200), bar));
            session.ApplyTracking(Snapshot(100, setup, bar));
            session.ApplyTracking(Snapshot(200, new(700, 200), bar));
            readyMs = 300;
        }
        else
        {
            bar = BarType.A3;
            setup = kind == TrainingStrategyKind.SnakeShot ? SnakePosition(Team.A) : Pull(Team.A);
            session.ApplyTracking(Snapshot(0, setup, bar));
            readyMs = 1000;
        }

        session.ApplyTracking(Snapshot(readyMs, setup, bar));
        TrainingFrameResult result = session.ApplyTracking(Snapshot(readyMs + 1000, setup, bar));
        Assert.Equal(expected, Assert.Single(result.Events.OfType<TrainingAnnouncementRequested>()).Variant!.Id);
        Assert.Equal(TrainingState.AwaitingExecution, result.State);
        Assert.Equal(expected, result.Variant!.Id);
        Assert.Equal(2, random.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tick_tack_setup_loss_aborts_once_and_requires_two_fresh_reversals(bool afterVariant)
    {
        TrainingSession session = Session(new([Option(TrainingStrategyKind.TickTackPass)]));
        session.ApplyTracking(Snapshot(0, new(700, 200), BarType.A5));
        session.ApplyTracking(Snapshot(100, new(700, 220), BarType.A5));
        session.ApplyTracking(Snapshot(200, new(700, 200), BarType.A5));
        session.ApplyTracking(Snapshot(300, new(700, 220), BarType.A5));
        if (afterVariant)
        {
            session.ApplyTracking(Snapshot(1300, new(700, 200), BarType.A5));
        }

        long exitMs = afterVariant ? 1400 : 400;
        session.ApplyTracking(Snapshot(exitMs, new(700, 450), BarType.A5));
        TrainingFrameResult result = session.ApplyTracking(Snapshot(exitMs + 750, new(700, 450), BarType.A5));
        Assert.Single(result.Events.OfType<TrainingAttemptAborted>());
        Assert.Equal(TrainingAnnouncementKind.Aborted, Assert.Single(result.Events.OfType<TrainingAnnouncementRequested>()).Kind);
        Assert.Empty(session.ApplyTracking(Snapshot(exitMs + 800, new(700, 450), BarType.A5)).Events);
        Assert.Empty(session.ApplyTracking(Snapshot(exitMs + 900, new(700, 200), BarType.A5)).Events);
        Assert.Empty(session.ApplyTracking(Snapshot(exitMs + 1000, new(700, 220), BarType.A5)).Events);
        Assert.Empty(session.ApplyTracking(Snapshot(exitMs + 1100, new(700, 200), BarType.A5)).Events);
        Assert.Single(session.ApplyTracking(Snapshot(exitMs + 1200, new(700, 220), BarType.A5)).Events.OfType<TrainingPreparationConfirmed>());
    }

    private static void ReadyAndAnnounce(TrainingSession session, Team team)
    {
        session.ApplyTracking(Snapshot(0, SnakePosition(team), Three(team)));
        session.ApplyTracking(Snapshot(1000, SnakePosition(team), Three(team)));
        session.ApplyTracking(Snapshot(2000, SnakePosition(team), Three(team)));
    }
}
