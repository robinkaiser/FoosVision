// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.UnitTests;

public class RecorderSettingsInitializerTests
{
    [Fact]
    public void Initialize_uses_preferred_path_when_writable()
    {
        FakeSettingsFileStore fileStore = new();
        RecorderSettingsInitializer testee = new(fileStore);

        RecorderSettingsContext context = testee.Initialize("external", "app-data");

        Assert.Equal("external", context.Paths.Root);
        Assert.Equal(Path.Combine("external", "Diagnostics"), context.Paths.Diagnostics.Root);
        Assert.Contains(context.Paths.Diagnostics.Logs, fileStore.CreatedDirectories);
        Assert.Contains(context.Paths.Diagnostics.Videos, fileStore.CreatedDirectories);
        Assert.Contains(context.Paths.ExampleConfig, fileStore.Files.Keys);
    }

    [Fact]
    public void Initialize_falls_back_to_app_data_when_preferred_path_is_not_writable()
    {
        FakeSettingsFileStore fileStore = new()
        {
            UnwritableDirectories = { "external" },
        };
        RecorderSettingsInitializer testee = new(fileStore);

        RecorderSettingsContext context = testee.Initialize("external", "app-data");

        Assert.Equal("app-data", context.Paths.Root);
        Assert.Equal(Path.Combine("app-data", "Diagnostics"), context.Paths.Diagnostics.Root);
    }

    [Fact]
    public void Initialize_returns_defaults_when_config_is_missing()
    {
        FakeSettingsFileStore fileStore = new();
        RecorderSettingsInitializer testee = new(fileStore);

        RecorderSettingsContext context = testee.Initialize("external", "app-data");

        Assert.Equal(SettingsConfigSource.DefaultsMissingConfig, context.ConfigSource);
        Assert.Equal(25, context.Settings.Viewer.LiveVideo.PlaybackBufferMilliseconds);
        Assert.Equal(100, context.Settings.Viewer.LiveVideo.MaxPlaybackBufferMilliseconds);
        Assert.True(context.Settings.Viewer.LiveVideo.DecoderLowLatency);
        Assert.Equal(2 * 1024 * 1024, context.Settings.Viewer.LiveVideo.UdpReceiveBufferBytes);
        Assert.True(context.Settings.Diagnostics.Logging.File.Enabled);
        Assert.False(context.Settings.Diagnostics.Logging.Seq.Enabled);
        Assert.False(context.Settings.Diagnostics.Video.Enabled);
        Assert.False(context.Settings.Diagnostics.RuntimeMetrics.Enabled);
        Assert.Equal(10, context.Settings.Diagnostics.RuntimeMetrics.ReportIntervalSeconds);
        Assert.False(context.Settings.Diagnostics.Vision.DebugVisualizations.ShowObservations);
        Assert.False(context.Settings.Diagnostics.Vision.DebugVisualizations.ShowBallDetectionMask);
        Assert.Null(context.ConfigError);
    }

    [Fact]
    public void Initialize_loads_existing_config()
    {
        FakeSettingsFileStore fileStore = new();
        string configPath = Path.Combine("external", "Config.json");
        fileStore.Files[configPath] = """
            {
              "version": 1,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": 50,
                  "maxPlaybackBufferMilliseconds": 200,
                  "decoderLowLatency": false,
                  "udpReceiveBufferBytes": 1048576
                }
              },
              "diagnostics": {
                "logging": {
                  "file": {
                    "enabled": false,
                    "minimumLevel": "Warning",
                    "format": "CompactJson",
                    "rollingInterval": "Day",
                    "retentionDays": 3,
                    "retainedFileCountLimit": 4
                  },
                  "seq": {
                    "enabled": true,
                    "serverUrl": "http://127.0.0.1:5341",
                    "apiKey": "secret",
                    "minimumLevel": "Debug",
                    "sendTestEventOnStartup": false
                  }
                },
                "video": {
                  "enabled": false,
                  "retentionDays": 2,
                  "maxTotalSizeBytes": 512
                },
                "runtimeMetrics": {
                  "enabled": true,
                  "reportIntervalSeconds": 7
                },
                "vision": {
                  "debugVisualizations": {
                    "showObservations": true,
                    "showBallDetectionMask": true
                  }
                }
              }
            }
            """;
        RecorderSettingsInitializer testee = new(fileStore);

        RecorderSettingsContext context = testee.Initialize("external", "app-data");

        Assert.Equal(SettingsConfigSource.ConfigFile, context.ConfigSource);
        Assert.Equal(50, context.Settings.Viewer.LiveVideo.PlaybackBufferMilliseconds);
        Assert.Equal(200, context.Settings.Viewer.LiveVideo.MaxPlaybackBufferMilliseconds);
        Assert.False(context.Settings.Viewer.LiveVideo.DecoderLowLatency);
        Assert.Equal(1048576, context.Settings.Viewer.LiveVideo.UdpReceiveBufferBytes);
        Assert.False(context.Settings.Diagnostics.Logging.File.Enabled);
        Assert.Equal("Warning", context.Settings.Diagnostics.Logging.File.MinimumLevel);
        Assert.True(context.Settings.Diagnostics.Logging.Seq.Enabled);
        Assert.Equal("secret", context.Settings.Diagnostics.Logging.Seq.ApiKey);
        Assert.False(context.Settings.Diagnostics.Video.Enabled);
        Assert.Equal(512, context.Settings.Diagnostics.Video.MaxTotalSizeBytes);
        Assert.True(context.Settings.Diagnostics.RuntimeMetrics.Enabled);
        Assert.Equal(7, context.Settings.Diagnostics.RuntimeMetrics.ReportIntervalSeconds);
        Assert.True(context.Settings.Diagnostics.Vision.DebugVisualizations.ShowObservations);
        Assert.True(context.Settings.Diagnostics.Vision.DebugVisualizations.ShowBallDetectionMask);
    }

    [Fact]
    public void Initialize_keeps_existing_config_when_invalid()
    {
        FakeSettingsFileStore fileStore = new();
        string configPath = Path.Combine("external", "Config.json");
        fileStore.Files[configPath] = "{ invalid";
        RecorderSettingsInitializer testee = new(fileStore);

        RecorderSettingsContext context = testee.Initialize("external", "app-data");

        Assert.Equal(SettingsConfigSource.DefaultsInvalidConfig, context.ConfigSource);
        Assert.Equal("{ invalid", fileStore.Files[configPath]);
        Assert.NotNull(context.ConfigError);
        Assert.True(context.Settings.Diagnostics.Logging.File.Enabled);
    }

    [Fact]
    public void Initialize_creates_config_json_from_example_when_missing()
    {
        FakeSettingsFileStore fileStore = new();
        RecorderSettingsInitializer testee = new(fileStore);

        RecorderSettingsContext context = testee.Initialize("external", "app-data");

        Assert.Contains(context.Paths.ExampleConfig, fileStore.Files.Keys);
        Assert.Contains(context.Paths.Config, fileStore.Files.Keys);
        Assert.Equal(fileStore.Files[context.Paths.ExampleConfig], fileStore.Files[context.Paths.Config]);
    }

    [Fact]
    public void Initialize_updates_example_config_without_overwriting_config_json()
    {
        FakeSettingsFileStore fileStore = new();
        string configPath = Path.Combine("external", "Config.json");
        string exampleConfigPath = Path.Combine("external", "Config.example.json");
        fileStore.Files[configPath] = """
            {
              "version": 1
            }
            """;
        string expectedConfig = fileStore.Files[configPath];
        fileStore.Files[exampleConfigPath] = "old example";
        RecorderSettingsInitializer testee = new(fileStore);

        testee.Initialize("external", "app-data");

        Assert.Equal(expectedConfig, fileStore.Files[configPath]);
        Assert.NotEqual("old example", fileStore.Files[exampleConfigPath]);
    }
}
