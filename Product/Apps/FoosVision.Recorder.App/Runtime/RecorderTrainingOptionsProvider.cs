// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.ValueObjects;
using FoosVision.Settings;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.Recorder.App.Runtime;

public class RecorderTrainingOptionsProvider : ITrainingOptionsProvider
{
    private readonly TrainingStrategyOptions[] _Strategies;

    public RecorderTrainingOptionsProvider()
        : this(RecorderLoggingBootstrap.CurrentSettings?.Settings.Training ?? RecorderTrainingSettings.CreateDefault())
    {
    }

    public RecorderTrainingOptionsProvider(RecorderTrainingSettings settings)
    {
        settings.Validate();
        _Strategies = MapStrategies(settings);
    }

    public TrainingOptions LoadTrainingOptions() => new(_Strategies);

    private static TrainingStrategyOptions[] MapStrategies(RecorderTrainingSettings settings)
    {
        IEnumerable<string> enabledStrategies = settings.EnabledStrategies.Count == 0
            ? RecorderTrainingSettings.CreateDefault().EnabledStrategies
            : settings.EnabledStrategies;
        return [.. enabledStrategies.Select(id => id switch
        {
            "snakeShot" => Map(TrainingStrategyKind.SnakeShot, settings.SnakeShot),
            "pullShot" => Map(TrainingStrategyKind.PullShot, settings.PullShot),
            "tickTackPass" => Map(TrainingStrategyKind.TickTackPass, settings.TickTackPass),
            _ => throw new InvalidOperationException($"Unknown training strategy '{id}'."),
        })];
    }

    private static TrainingStrategyOptions Map(TrainingStrategyKind kind, TrainingStrategySettings settings)
    {
        WaitDistribution distribution = settings.WaitDistribution switch
        {
            "uniform" => WaitDistribution.Uniform,
            "preferLong" => WaitDistribution.PreferLong,
            "preferShort" => WaitDistribution.PreferShort,
            _ => throw new InvalidOperationException("Unknown training wait distribution."),
        };
        return new(kind, new(settings.MinWaitSeconds, settings.MaxWaitSeconds, distribution),
            settings.Variants.Select(variant => new TrainingVariant(variant.Id, variant.Text)),
            TrainingVariantSelection.RandomWithRepeats);
    }
}
