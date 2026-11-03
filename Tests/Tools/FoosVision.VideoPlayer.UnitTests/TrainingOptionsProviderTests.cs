// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.ValueObjects;
using FoosVision.Settings;
using FoosVision.VideoPlayer.Runtime;

namespace FoosVision.VideoPlayer.UnitTests;

public class TrainingOptionsProviderTests
{
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
