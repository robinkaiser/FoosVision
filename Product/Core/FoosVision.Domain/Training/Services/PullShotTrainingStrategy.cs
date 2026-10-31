// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public class PullShotTrainingStrategy : ShotTrainingStrategy
{
    private const double _MinSideFraction = 0.25;
    private const double _MaxSideFraction = 0.35;

    public PullShotTrainingStrategy(TrainingStrategyOptions options, TableConfiguration tableConfig) : base(options, tableConfig)
    {
    }

    public override string ReadyText => "Pull ready";
    public override IReadOnlyList<TrainingVariant> Variants => Options.Variants;
    protected override bool RequiresRest => true;

    protected override bool IsInSetup(LiveTrackingSnapshot snapshot, Team team)
    {
        double side = TrainingGeometry.SideFraction(TableConfig, TrainingGeometry.ThreeBar(team), snapshot.BallPosition);
        if (team == Team.B)
        {
            side = 1 - side;
        }

        return
            snapshot.Possession == new BallPossession(team, PossessionArea.ThreeBar) &&
            side >= _MinSideFraction &&
            side <= _MaxSideFraction;
    }
}
