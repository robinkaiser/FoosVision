// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.UnitTests.Training;

public class TrainingOptionsTests
{
    [Fact]
    public void Variant_announcement_defaults_to_onReady_and_rejects_unknown_modes()
    {
        Assert.Equal(TrainingVariantAnnouncement.OnReady, TrainingOptions.Default.VariantAnnouncement);
        Assert.Throws<ArgumentOutOfRangeException>(() => new TrainingOptions(
            TrainingOptions.Default.Strategies, (TrainingVariantAnnouncement)99));
    }

    [Theory]
    [InlineData(0, 2, 2, 6)]
    [InlineData(0.25, 3, 4, 4)]
    [InlineData(0.81, 5.24, 5.6, 2.4)]
    public void Wait_distributions_have_deterministic_values(double unit, double uniform, double preferLong, double preferShort)
    {
        Assert.Equal(uniform, new TrainingWaitOptions(2, 6, WaitDistribution.Uniform).Select(unit).TotalSeconds, 6);
        Assert.Equal(preferLong, new TrainingWaitOptions(2, 6, WaitDistribution.PreferLong).Select(unit).TotalSeconds, 6);
        Assert.Equal(preferShort, new TrainingWaitOptions(2, 6, WaitDistribution.PreferShort).Select(unit).TotalSeconds, 6);
    }

    [Fact]
    public void Distribution_bounds_monotonicity_mirroring_and_fixed_wait()
    {
        double previousLong = 2;
        double previousShort = 6;
        for (int i = 0; i < 100; i++)
        {
            double unit = i / 100.0;
            double longer = new TrainingWaitOptions(2, 6, WaitDistribution.PreferLong).Select(unit).TotalSeconds;
            double shorter = new TrainingWaitOptions(2, 6, WaitDistribution.PreferShort).Select(unit).TotalSeconds;
            Assert.InRange(longer, previousLong, 6);
            Assert.InRange(shorter, 2, previousShort);
            Assert.Equal(8, longer + shorter, 6);
            Assert.Equal(3, new TrainingWaitOptions(3, 3, WaitDistribution.PreferLong).Select(unit).TotalSeconds);
            previousLong = longer;
            previousShort = shorter;
        }
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(0, double.PositiveInfinity)]
    public void Invalid_waits_are_rejected(double min, double max)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new TrainingWaitOptions(min, max, WaitDistribution.Uniform));

    [Fact]
    public void Strategy_options_snapshot_variants_and_reject_invalid_variants()
    {
        List<TrainingVariant> variants = [new("right", "Rechts", 1)];
        TrainingStrategyOptions snake = new(TrainingStrategyKind.SnakeShot, new(1, 2, WaitDistribution.Uniform), variants);
        variants.Clear();
        Assert.Equal(new TrainingVariant("right", "Rechts", 1), Assert.Single(snake.Variants));
        Assert.Throws<ArgumentException>(() => new TrainingOptions([
            new(TrainingStrategyKind.SnakeShot, snake.Wait, [new("wall", "Wall", 1)])]));
        Assert.Throws<ArgumentException>(() => new TrainingOptions([
            new(TrainingStrategyKind.PullShot, snake.Wait, [new("short", " ", 1)])]));
        Assert.Throws<ArgumentException>(() => new TrainingOptions([
            new(TrainingStrategyKind.TickTackPass, snake.Wait, [])]));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Invalid_variant_weights_are_rejected(double weight)
    {
        Assert.Throws<ArgumentException>(() => new TrainingOptions([
            new(TrainingStrategyKind.SnakeShot, new(1, 1, WaitDistribution.Uniform), [new("left", "Left", weight)])]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(double.MaxValue)]
    public void Zero_or_overflowing_weight_sum_is_rejected(double weight)
    {
        Assert.Throws<ArgumentException>(() => new TrainingOptions([
            new(TrainingStrategyKind.SnakeShot, new(1, 1, WaitDistribution.Uniform),
                [new("left", "Left", weight), new("right", "Right", weight)])]));
    }

    [Fact]
    public void Enabled_options_are_a_snapshot_and_must_be_unique_and_nonempty()
    {
        List<TrainingStrategyOptions> enabled = [TrainingTestData.Option(TrainingStrategyKind.SnakeShot)];
        TrainingOptions options = new(enabled);
        enabled.Clear();
        Assert.Single(options.Strategies);
        Assert.Throws<ArgumentException>(() => new TrainingOptions([]));
        Assert.Throws<ArgumentException>(() => new TrainingOptions([options.Strategies[0], options.Strategies[0]]));
        Assert.Throws<ArgumentException>(() => new TrainingOptions([TrainingTestData.Option((TrainingStrategyKind)99)]));
    }
}
