// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision.Strategies;

namespace FoosVision.Domain.TrackingCore.Services.ReplayDecision;

public static class ReplayDeciderFactory
{
    public static IReplayDecider CreateDefault(TableConfiguration tableConfiguration)
    {
        return new ReplayDecider(CreateDefaultStrategies(tableConfiguration));
    }

    private static IReadOnlyList<IReplayDecisionStrategy> CreateDefaultStrategies(TableConfiguration tableConfiguration)
    {
        TableImageScale tableImageScale = TableImageScale.From(tableConfiguration);

        return
        [
            new BallDisappearedReplayStrategy(tableConfiguration),
            new SavedShotReplayStrategy(tableImageScale),
        ];
    }
}
