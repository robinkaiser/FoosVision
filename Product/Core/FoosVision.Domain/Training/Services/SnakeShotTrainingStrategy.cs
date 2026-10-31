// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public class SnakeShotTrainingStrategy : ShotTrainingStrategy
{
    private const double _MinSideFraction = 0.425;
    private const double _MaxSideFraction = 0.575;
    private const double _MinAttackFraction = 0.20;
    private const double _MaxAttackFraction = 0.35;

    public SnakeShotTrainingStrategy(TrainingStrategyOptions options, TableConfiguration tableConfig) : base(options, tableConfig)
    {
    }

    public override string ReadyText => "Snake ready";
    public override IReadOnlyList<TrainingVariant> Variants => Options.Variants;
    protected override bool RequiresRest => false;

    protected override bool IsInSetup(LiveTrackingSnapshot snapshot, Team team)
    {
        BarType bar = TrainingGeometry.ThreeBar(team);
        double side = TrainingGeometry.SideFraction(TableConfig, bar, snapshot.BallPosition);
        double front = TrainingGeometry.AttackProgress(TableConfig, team, snapshot.BallPosition, bar);

        return
            snapshot.Possession == new BallPossession(team, PossessionArea.ThreeBar) &&
            side >= _MinSideFraction &&
            side <= _MaxSideFraction &&
            front >= _MinAttackFraction &&
            front <= _MaxAttackFraction;
    }
}
