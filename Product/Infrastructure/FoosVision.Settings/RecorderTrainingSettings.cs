// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json.Serialization;

namespace FoosVision.Settings;

public class RecorderTrainingSettings
{
    [JsonRequired]
    public string VariantAnnouncement { get; set; } = "onReady";

    public List<string> EnabledStrategies { get; set; } = ["snakeShot", "pullShot", "pass"];
    public SnakeShotSettings SnakeShot { get; set; } = new();
    public PullShotSettings PullShot { get; set; } = new();
    public PassSettings Pass { get; set; } = new();

    public static RecorderTrainingSettings CreateDefault()
    {
        return new RecorderTrainingSettings();
    }

    public void Validate()
    {
        if (VariantAnnouncement is not ("onReady" or "onShoot"))
        {
            throw new InvalidOperationException("training.variantAnnouncement must be onReady or onShoot.");
        }

        if (EnabledStrategies is null || EnabledStrategies.Any(id => id is not ("snakeShot" or "pullShot" or "pass")) ||
            EnabledStrategies.Distinct(StringComparer.Ordinal).Count() != EnabledStrategies.Count)
        {
            throw new InvalidOperationException("training.enabledStrategies must contain known, unique strategy IDs.");
        }

        if (SnakeShot is null || PullShot is null || Pass is null)
        {
            throw new InvalidOperationException("Training strategy sections must not be null.");
        }

        SnakeShot.Validate(["left", "right", "middle"]);
        PullShot.Validate(["short", "middle", "long"]);
        Pass.Validate(["wall", "lane", "split"]);
    }
}

public class TrainingVariantSettings(string id, string text, double weight)
{
    [JsonRequired]
    public string Id { get; set; } = id;

    [JsonRequired]
    public string Text { get; set; } = text;

    [JsonRequired]
    public double Weight { get; set; } = weight;
}

public abstract class TrainingStrategySettings
{
    public double MinWaitSeconds { get; set; } = 1;
    public double MaxWaitSeconds { get; set; } = 10;
    public string WaitDistribution { get; set; } = "uniform";
    public List<TrainingVariantSettings> Variants { get; set; } = [];

    public void Validate(string[] allowedVariants)
    {
        if (!double.IsFinite(MinWaitSeconds) || !double.IsFinite(MaxWaitSeconds) || MinWaitSeconds < 0 ||
            MaxWaitSeconds < MinWaitSeconds || MaxWaitSeconds > long.MaxValue / 1_000_000_000.0)
        {
            throw new InvalidOperationException("Training wait seconds must be finite, nonnegative, min <= max and representable in frame nanoseconds.");
        }

        if (WaitDistribution is not ("uniform" or "preferLong" or "preferShort"))
        {
            throw new InvalidOperationException("Unsupported training wait distribution.");
        }

        if (Variants is null || Variants.Count == 0 ||
            Variants.Any(variant => variant is null || !allowedVariants.Contains(variant.Id, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(variant.Text) ||
                !double.IsFinite(variant.Weight) || variant.Weight < 0) ||
            Variants.Select(variant => variant.Id).Distinct(StringComparer.Ordinal).Count() != Variants.Count ||
            !double.IsFinite(Variants.Sum(variant => variant.Weight)) || Variants.Sum(variant => variant.Weight) <= 0)
        {
            throw new InvalidOperationException("Training variants must be nonempty, unique, valid for their strategy, and have nonempty texts and finite nonnegative weights with a finite positive sum.");
        }
    }
}

public class SnakeShotSettings : TrainingStrategySettings
{
    public SnakeShotSettings()
    {
        MaxWaitSeconds = 12;
        Variants = [new("left", "Left", 1), new("right", "Right", 1), new("middle", "Middle", 1)];
    }
}

public class PullShotSettings : TrainingStrategySettings
{
    public PullShotSettings()
    {
        Variants = [new("short", "Short", 1), new("middle", "Middle", 1), new("long", "Long", 1)];
    }
}

public class PassSettings : TrainingStrategySettings
{
    public PassSettings()
    {
        Variants = [new("wall", "Wall", 1), new("lane", "Lane", 1), new("split", "Split", 1)];
    }
}
