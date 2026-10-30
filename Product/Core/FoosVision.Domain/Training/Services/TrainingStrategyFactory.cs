// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public static class TrainingStrategyFactory
{
    public static IReadOnlyList<ITrainingStrategy> Create(TrainingOptions options, TableConfiguration tableConfig)
        => [.. options.Strategies.Select(option => Create(option, tableConfig))];

    private static ITrainingStrategy Create(TrainingStrategyOptions option, TableConfiguration tableConfig)
        => option.Kind switch
        {
            TrainingStrategyKind.SnakeShot => new SnakeShotTrainingStrategy(option, tableConfig),
            TrainingStrategyKind.PullShot => new PullShotTrainingStrategy(option, tableConfig),
            TrainingStrategyKind.TickTackPass => new TickTackPassTrainingStrategy(option, tableConfig),
            _ => throw new ArgumentOutOfRangeException(nameof(option)),
        };
}
