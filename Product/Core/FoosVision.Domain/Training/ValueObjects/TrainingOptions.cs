// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.Training.ValueObjects;

public enum TrainingStrategyKind
{
    SnakeShot,
    PullShot,
    TickTackPass,
}

public enum WaitDistribution
{
    Uniform,
    PreferLong,
    PreferShort,
}

public interface ITrainingRandom
{
    /// <summary>Returns a finite value in [0, 1).</summary>
    double NextUnit();
}

public record TrainingWaitOptions
{
    public TrainingWaitOptions(double minSeconds, double maxSeconds, WaitDistribution distribution)
    {
        if (!double.IsFinite(minSeconds) || !double.IsFinite(maxSeconds) ||
            minSeconds < 0 || maxSeconds < minSeconds || maxSeconds > long.MaxValue / 1_000_000_000.0)
        {
            throw new ArgumentOutOfRangeException(nameof(minSeconds));
        }

        if (!Enum.IsDefined(distribution))
        {
            throw new ArgumentOutOfRangeException(nameof(distribution));
        }

        MinSeconds = minSeconds;
        MaxSeconds = maxSeconds;
        Distribution = distribution;
    }

    public double MinSeconds { get; }
    public double MaxSeconds { get; }
    public WaitDistribution Distribution { get; }

    public TimeSpan Select(double unit)
    {
        ValidateUnit(unit);
        double fraction = Distribution switch
        {
            WaitDistribution.PreferLong => Math.Sqrt(unit),
            WaitDistribution.PreferShort => 1 - Math.Sqrt(unit),
            _ => unit,
        };
        return TimeSpan.FromSeconds(MinSeconds + ((MaxSeconds - MinSeconds) * fraction));
    }

    internal static void ValidateUnit(double unit)
    {
        if (!double.IsFinite(unit) || unit < 0 || unit >= 1)
        {
            throw new ArgumentOutOfRangeException(nameof(unit));
        }
    }
}

public enum TrainingVariantSelection
{
    RandomWithRepeats,
}

public record TrainingStrategyOptions
{
    public TrainingStrategyOptions(TrainingStrategyKind kind, TrainingWaitOptions wait,
        IEnumerable<TrainingVariant>? variants = null,
        TrainingVariantSelection variantSelection = TrainingVariantSelection.RandomWithRepeats)
    {
        Kind = kind;
        Wait = wait;
        VariantSelection = variantSelection;
        Variants = Array.AsReadOnly((variants ?? DefaultVariants(kind)).ToArray());
    }

    public TrainingStrategyKind Kind { get; }
    public TrainingWaitOptions Wait { get; }
    public IReadOnlyList<TrainingVariant> Variants { get; }
    public TrainingVariantSelection VariantSelection { get; }

    internal void Validate()
    {
        string[] allowed = [.. DefaultVariants(Kind).Select(variant => variant.Id)];
        if (Wait is null || !Enum.IsDefined(VariantSelection) || Variants.Count == 0 ||
            Variants.Any(variant => variant is null || !allowed.Contains(variant.Id, StringComparer.Ordinal) || string.IsNullOrWhiteSpace(variant.Text)) ||
            Variants.Select(variant => variant.Id).Distinct(StringComparer.Ordinal).Count() != Variants.Count)
        {
            throw new ArgumentException("Invalid strategy variants or selection.");
        }
    }

    private static TrainingVariant[] DefaultVariants(TrainingStrategyKind kind) => kind switch
    {
        TrainingStrategyKind.SnakeShot => [new("push", "Push"), new("pull", "Pull"), new("middle", "Middle")],
        TrainingStrategyKind.PullShot => [new("short", "Short"), new("middle", "Middle"), new("long", "Long")],
        _ => [new("wall", "Wall"), new("lane", "Lane"), new("split", "Split")],
    };
}

public class TrainingOptions
{
    public TrainingOptions(IEnumerable<TrainingStrategyOptions> strategies)
    {
        TrainingStrategyOptions[] snapshot = [.. strategies];
        if (snapshot.Length == 0 || snapshot.Any(option => option is null || option.Wait is null || !Enum.IsDefined(option.Kind)) ||
            snapshot.Select(option => option.Kind).Distinct().Count() != snapshot.Length)
        {
            throw new ArgumentException("Enable at least one strategy, without duplicates or unknown kinds.", nameof(strategies));
        }

        foreach (TrainingStrategyOptions option in snapshot)
        {
            option.Validate();
        }

        Strategies = Array.AsReadOnly(snapshot);
    }

    public IReadOnlyList<TrainingStrategyOptions> Strategies { get; }

    public static TrainingOptions Default => new(
    [
        new(TrainingStrategyKind.SnakeShot, new(1, 12, WaitDistribution.Uniform)),
        new(TrainingStrategyKind.PullShot, new(1, 10, WaitDistribution.Uniform)),
        new(TrainingStrategyKind.TickTackPass, new(1, 10, WaitDistribution.Uniform)),
    ]);
}
