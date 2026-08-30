// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.UnitTests;

public class ViewerSettingsInitializerTests
{
    [Fact]
    public void Initialize_creates_logs_directory_without_config_files()
    {
        FakeSettingsFileStore fileStore = new();
        ViewerSettingsInitializer testee = new(fileStore);

        ViewerSettingsContext context = testee.Initialize("external", "app-data");

        Assert.Equal("external", context.Paths.Root);
        Assert.Equal(Path.Combine("external", "Diagnostics"), context.Paths.Diagnostics.Root);
        Assert.Contains(context.Paths.Diagnostics.Logs, fileStore.CreatedDirectories);
        Assert.False(context.Settings.Diagnostics.RuntimeMetrics.Enabled);
        Assert.Equal(10, context.Settings.Diagnostics.RuntimeMetrics.ReportIntervalSeconds);
        Assert.DoesNotContain(context.Paths.Config, fileStore.Files.Keys);
        Assert.DoesNotContain(context.Paths.ExampleConfig, fileStore.Files.Keys);
    }

    [Fact]
    public void Initialize_falls_back_to_app_data_when_preferred_path_is_not_writable()
    {
        FakeSettingsFileStore fileStore = new()
        {
            UnwritableDirectories = { "external" },
        };
        ViewerSettingsInitializer testee = new(fileStore);

        ViewerSettingsContext context = testee.Initialize("external", "app-data");

        Assert.Equal("app-data", context.Paths.Root);
        Assert.Equal(Path.Combine("app-data", "Diagnostics"), context.Paths.Diagnostics.Root);
    }
}
