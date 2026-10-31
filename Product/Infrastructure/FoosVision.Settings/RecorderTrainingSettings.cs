// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings;

public class RecorderTrainingSettings
{
    public List<string> EnabledStrategies { get; set; } = ["snakeShot", "pullShot", "tickTackPass"];
    public SnakeShotSettings SnakeShot { get; set; } = new();
    public PullShotSettings PullShot { get; set; } = new();
    public TickTackPassSettings TickTackPass { get; set; } = new();

    public static RecorderTrainingSettings CreateDefault()
    {
        return new RecorderTrainingSettings();
    }

    public void Validate()
    {
        if (EnabledStrategies is null || EnabledStrategies.Any(id => id is not ("snakeShot" or "pullShot" or "tickTackPass")) ||
            EnabledStrategies.Distinct(StringComparer.Ordinal).Count() != EnabledStrategies.Count)
        {
            throw new InvalidOperationException("training.enabledStrategies must contain known, unique strategy IDs.");
        }

        if (SnakeShot is null || PullShot is null || TickTackPass is null)
        {
            throw new InvalidOperationException("Training strategy sections must not be null.");
        }

        SnakeShot.Validate(["push", "pull", "middle"]);
        PullShot.Validate(["short", "middle", "long"]);
        TickTackPass.Validate(["wall", "lane", "split"]);
    }
}

public class TrainingVariantSettings(string id, string text)
{
    public string Id { get; set; } = id;
    public string Text { get; set; } = text;
}

public abstract class TrainingStrategySettings
{
    public double MinWaitSeconds { get; set; } = 1;
    public double MaxWaitSeconds { get; set; } = 10;
    public string WaitDistribution { get; set; } = "uniform";
    public string VariantSelection { get; set; } = "randomWithRepeats";
    public List<TrainingVariantSettings> Variants { get; set; } = [];

    public void Validate(string[] allowedVariants)
    {
        if (!double.IsFinite(MinWaitSeconds) || !double.IsFinite(MaxWaitSeconds) || MinWaitSeconds < 0 ||
            MaxWaitSeconds < MinWaitSeconds || MaxWaitSeconds > long.MaxValue / 1_000_000_000.0)
        {
            throw new InvalidOperationException("Training wait seconds must be finite, nonnegative, min <= max and representable in frame nanoseconds.");
        }

        if (WaitDistribution is not ("uniform" or "preferLong" or "preferShort") || VariantSelection != "randomWithRepeats")
        {
            throw new InvalidOperationException("Unsupported training wait distribution or variant selection.");
        }

        if (Variants is null || Variants.Count == 0 ||
            Variants.Any(variant => variant is null || !allowedVariants.Contains(variant.Id, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(variant.Text)) ||
            Variants.Select(variant => variant.Id).Distinct(StringComparer.Ordinal).Count() != Variants.Count)
        {
            throw new InvalidOperationException("Training variants must be nonempty, unique, valid for their strategy and have nonempty texts.");
        }
    }
}

public class SnakeShotSettings : TrainingStrategySettings
{
    public SnakeShotSettings()
    {
        MaxWaitSeconds = 12;
        Variants = [new("push", "Push"), new("pull", "Pull"), new("middle", "Middle")];
    }
}

public class PullShotSettings : TrainingStrategySettings
{
    public PullShotSettings()
    {
        Variants = [new("short", "Short"), new("middle", "Middle"), new("long", "Long")];
    }
}

public class TickTackPassSettings : TrainingStrategySettings
{
    public TickTackPassSettings()
    {
        Variants = [new("wall", "Wall"), new("lane", "Lane"), new("split", "Split")];
    }
}
