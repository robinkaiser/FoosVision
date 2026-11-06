// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.ValueObjects;
using FoosVision.Settings;
using FoosVision.VideoPlayer.Runtime;

namespace FoosVision.VideoPlayer.UnitTests;

public class TrainingOptionsProviderTests
{
    [Fact]
    public void Variant_weights_are_mapped_and_snapshotted()
    {
        RecorderTrainingSettings settings = new();
        settings.SnakeShot.Variants[0].Weight = 0;
        settings.SnakeShot.Variants[1].Weight = 2.5;
        VideoPlayerTrainingOptionsProvider provider = new(settings);
        settings.SnakeShot.Variants[1].Weight = 99;
        Assert.Equal(new double[] { 0, 2.5, 1 }, provider.LoadTrainingOptions().Strategies[0].Variants.Select(variant => variant.Weight));
    }

    [Theory]
    [InlineData("onReady", TrainingVariantAnnouncement.OnReady)]
    [InlineData("onShoot", TrainingVariantAnnouncement.OnShoot)]
    public void Variant_announcement_is_mapped_and_snapshotted(string value, TrainingVariantAnnouncement expected)
    {
        RecorderTrainingSettings settings = new() { VariantAnnouncement = value };
        VideoPlayerTrainingOptionsProvider provider = new(settings);
        settings.VariantAnnouncement = "invalid";
        Assert.Equal(expected, provider.LoadTrainingOptions().VariantAnnouncement);
        Assert.Equal(expected, provider.LoadTrainingOptions().VariantAnnouncement);
    }
}
