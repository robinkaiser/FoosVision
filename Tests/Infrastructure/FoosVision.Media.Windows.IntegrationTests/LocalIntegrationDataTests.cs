// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Windows.IntegrationTests;

public class LocalIntegrationDataTests
{
    [Fact]
    public void Missing_integration_data_is_skipped()
    {
        string missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        Assert.True(LocalIntegrationData.ShouldSkipFile(missingPath));
        Assert.True(LocalIntegrationData.ShouldSkipDirectory(missingPath));
    }
}
