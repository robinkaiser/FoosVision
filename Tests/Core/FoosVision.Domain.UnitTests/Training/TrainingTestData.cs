// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.ValueObjects;
using NSubstitute;

namespace FoosVision.Domain.UnitTests.Training;

public static class TrainingTestData
{
    public static Frame At(long ms) => new((ulong)(ms + 1), ms * 1_000_000L);

    public static Point SnakePosition(Team team) => team == Team.A ? new(1150, 450) : new(450, 450);
    public static Point Pull(Team team) => team == Team.A ? new(1100, 318) : new(500, 582);
    public static BarType Three(Team team) => team == Team.A ? BarType.A3 : BarType.B3;
    public static BarType Five(Team team) => team == Team.A ? BarType.A5 : BarType.B5;
    public static BarType Target(Team team) => team == Team.A ? BarType.B2 : BarType.A2;

    public static TrainingStrategyOptions Option(TrainingStrategyKind kind, double min = 1, double max = 1)
        => new(kind, new(min, max, WaitDistribution.Uniform));

    public static LiveTrackingSnapshot Snapshot(long ms, Point position, BarType bar = BarType.A3, Vector2 velocity = default,
        LiveTrackingPhase phase = LiveTrackingPhase.Live)
    {
        Frame frame = At(ms);
        bool found = phase == LiveTrackingPhase.Live;
        BallPossession possession = new(TableBarClassifier.GetTeam(bar), TableBarClassifier.GetPossessionArea(bar));
        TrackedBall ball = new(1, frame, position, TrackingConfidence.High, TrackingStatus.Observed, velocity);
        return new(frame, phase, found, position, velocity, found ? [ball] : [],
            phase == LiveTrackingPhase.BallLost ? BallPossession.None : possession, possession, 0, 0, found ? bar : null);
    }

    public static TrainingSession Session(TrainingOptions? options = null, CountingRandom? random = null)
    {
        LiveTracker tracker = new(LiveTrackerParams.Default, Substitute.For<IBallTracker>(), Substitute.For<IPossessionCalculator>());
        return new(Guid.NewGuid(), tracker, TableConfig.Config,
            options ?? new([Option(TrainingStrategyKind.SnakeShot)]), random ?? new());
    }
}

public class CountingRandom(params double[] values) : ITrainingRandom
{
    private readonly Queue<double> _Values = new(values);
    public int Calls { get; private set; }

    public double NextUnit()
    {
        Calls++;
        return _Values.Count > 0 ? _Values.Dequeue() : 0;
    }
}
