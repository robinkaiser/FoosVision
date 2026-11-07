// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.Events;
using FoosVision.Domain.Training.Services;
using FoosVision.Domain.Training.ValueObjects;
using static FoosVision.Domain.UnitTests.Training.TrainingTestData;

namespace FoosVision.Domain.UnitTests.Training;

public class TrainingReplayTests
{
    [Theory]
    [InlineData(TrainingStrategyKind.SnakeShot, TrainingVariantAnnouncement.OnReady)]
    [InlineData(TrainingStrategyKind.SnakeShot, TrainingVariantAnnouncement.OnShoot)]
    [InlineData(TrainingStrategyKind.PullShot, TrainingVariantAnnouncement.OnReady)]
    [InlineData(TrainingStrategyKind.PullShot, TrainingVariantAnnouncement.OnShoot)]
    [InlineData(TrainingStrategyKind.TickTackPass, TrainingVariantAnnouncement.OnReady)]
    [InlineData(TrainingStrategyKind.TickTackPass, TrainingVariantAnnouncement.OnShoot)]
    public void Completed_technique_replays_until_any_enabled_strategy_confirms_new_preparation(
        TrainingStrategyKind kind, TrainingVariantAnnouncement mode)
    {
        TrainingSession session = Session(new([Option(kind), Option(kind == TrainingStrategyKind.TickTackPass
            ? TrainingStrategyKind.SnakeShot : TrainingStrategyKind.TickTackPass)], mode));
        TrainingAttemptCompleted completed;
        if (kind == TrainingStrategyKind.TickTackPass)
        {
            PreparePass(session, 0);
            for (long ms = 400; ms <= 1300; ms += 100)
            {
                session.ApplyTracking(Snapshot(ms, new(700, 200 + ms / 1000.0), BarType.A5));
            }
            completed = Assert.Single(session.ApplyTracking(Snapshot(1400, new(1100, 250), BarType.A3))
                .Events.OfType<TrainingAttemptCompleted>());
        }
        else
        {
            Point setup = kind == TrainingStrategyKind.SnakeShot ? SnakePosition(Team.A) : Pull(Team.A);
            session.ApplyTracking(Snapshot(0, setup));
            session.ApplyTracking(Snapshot(1000, setup));
            for (long ms = 1100; ms <= 2000; ms += 100)
            {
                session.ApplyTracking(Snapshot(ms, setup with { Y = setup.Y + ms / 1000.0 }));
            }
            session.ApplyTracking(Snapshot(2100, new(1200, 450), velocity: new(2000, 0)));
            session.ApplyTracking(Snapshot(2200, new(1300, 450), BarType.B2));
            completed = Assert.Single(session.ApplyTracking(Snapshot(2450, new(1300, 450), BarType.B2))
                .Events.OfType<TrainingAttemptCompleted>());
        }

        long anchorMs = kind == TrainingStrategyKind.TickTackPass ? 1100 : 1800;
        Point anchorPosition = kind == TrainingStrategyKind.TickTackPass ? new(700, 201.1)
            : (kind == TrainingStrategyKind.SnakeShot ? SnakePosition(Team.A) : Pull(Team.A));
        if (kind != TrainingStrategyKind.TickTackPass)
        {
            anchorPosition = anchorPosition with { Y = anchorPosition.Y + 1.8 };
        }
        Assert.Equal(At(anchorMs), completed.Attempt.Anchor.Frame);
        Assert.Equal(anchorPosition, completed.Attempt.Anchor.Position);
        ReplayPlan replay = TrainingReplayRules.Create(completed);
        Assert.Equal(completed.Attempt.Anchor.Frame, replay.AnchorFrame);
        Assert.Equal(completed.Attempt.Anchor.Position, replay.AnchorPosition);
        Assert.Equal(completed.Attempt.Anchor.Possession, replay.AnchorPossession);
        Assert.Equal(replay.AnchorFrame.TimestampNs - 300_000_000, replay.StartTimestampNs);
        Assert.Equal(completed.ConfirmationFrame.TimestampNs + 300_000_000, replay.EndTimestampNs);
        Assert.Equal(0.25, replay.Speed);
        Assert.Equal(1, replay.RequiredCompletedLoops);

        // Even beyond Game's return delay, a moving ball on the three-bar does not end Training replay.
        session.ApplyTracking(Snapshot(9000, new(1100, 800), BarType.A3, new(500, 0)));
        TrainingFrameResult moving = session.ApplyTracking(Snapshot(9100, new(1150, 800), BarType.A3, new(500, 0)));
        Assert.Empty(moving.Events);

        TrainingFrameResult ready;
        if (kind == TrainingStrategyKind.TickTackPass)
        {
            session.ApplyTracking(Snapshot(10000, SnakePosition(Team.A)));
            ready = session.ApplyTracking(Snapshot(11000, SnakePosition(Team.A)));
        }
        else
        {
            ready = PreparePass(session, 10000);
        }

        TrainingReturnToLiveRequested request = Assert.Single(ready.Events.OfType<TrainingReturnToLiveRequested>());
        Assert.Equal(completed.CompletionFrame, request.ReplayTriggerFrame);
        Assert.Equal(completed.Attempt, request.Attempt);
        Assert.Single(ready.Events.OfType<TrainingPreparationConfirmed>());
        Assert.Equal(TrainingAnnouncementKind.Ready, Assert.Single(ready.Events.OfType<TrainingAnnouncementRequested>()).Kind);
        session.Stop();
        Assert.Empty(session.ApplyTracking(Snapshot(12000, SnakePosition(Team.A))).Events);
    }

    private static TrainingFrameResult PreparePass(TrainingSession session, long startMs)
    {
        session.ApplyTracking(Snapshot(startMs, new(700, 200), BarType.A5));
        session.ApplyTracking(Snapshot(startMs + 100, new(700, 220), BarType.A5));
        session.ApplyTracking(Snapshot(startMs + 200, new(700, 200), BarType.A5));
        return session.ApplyTracking(Snapshot(startMs + 300, new(700, 220), BarType.A5));
    }
}
