// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.ValidationTests;

public class LocalValidationDataTests
{
    [Fact]
    public void Missing_validation_data_is_skipped()
    {
        string missingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.True(LocalValidationData.ShouldSkipDirectory(missingDirectory));
    }
}
