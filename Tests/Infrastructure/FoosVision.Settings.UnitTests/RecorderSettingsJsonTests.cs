// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json;

namespace FoosVision.Settings.UnitTests;

public class RecorderSettingsJsonTests
{
    [Fact]
    public void DeserializeAndValidate_returns_settings_when_json_is_valid()
    {
        string json = """
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
                "runtimeMetrics": {
                  "enabled": true,
                  "reportIntervalSeconds": 5
                }
              }
            }
            """;

        RecorderSettings settings = RecorderSettingsJson.DeserializeAndValidate(json);

        Assert.Equal(50, settings.Viewer.LiveVideo.PlaybackBufferMilliseconds);
        Assert.Equal(200, settings.Viewer.LiveVideo.MaxPlaybackBufferMilliseconds);
        Assert.False(settings.Viewer.LiveVideo.DecoderLowLatency);
        Assert.Equal(1048576, settings.Viewer.LiveVideo.UdpReceiveBufferBytes);
        Assert.True(settings.Diagnostics.RuntimeMetrics.Enabled);
        Assert.Equal(5, settings.Diagnostics.RuntimeMetrics.ReportIntervalSeconds);
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_json_is_malformed()
    {
        JsonException ex = Assert.Throws<JsonException>(
            () => RecorderSettingsJson.DeserializeAndValidate("{ invalid"));

        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_version_is_invalid()
    {
        string json = """
            {
              "version": 99,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": 25,
                  "maxPlaybackBufferMilliseconds": 100,
                  "decoderLowLatency": true,
                  "udpReceiveBufferBytes": 2097152
                }
              },
              "diagnostics": {
              }
            }
            """;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => RecorderSettingsJson.DeserializeAndValidate(json));

        Assert.Contains("Unsupported settings config version", ex.Message);
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_viewer_section_is_missing()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
              }
            }
            """;

        AssertInvalidJson(json, "'viewer'");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_live_video_section_is_missing()
    {
        string json = """
            {
              "version": 1,
              "viewer": {
              },
              "diagnostics": {
              }
            }
            """;

        AssertInvalidJson(json, "'liveVideo'");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_live_video_parameter_is_missing()
    {
        string json = """
            {
              "version": 1,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": 25,
                  "maxPlaybackBufferMilliseconds": 100,
                  "decoderLowLatency": true
                }
              },
              "diagnostics": {
              }
            }
            """;

        AssertInvalidJson(json, "'udpReceiveBufferBytes'");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_diagnostics_section_is_missing()
    {
        string json = """
            {
              "version": 1,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": 25,
                  "maxPlaybackBufferMilliseconds": 100,
                  "decoderLowLatency": true,
                  "udpReceiveBufferBytes": 2097152
                }
              }
            }
            """;

        AssertInvalidJson(json, "'diagnostics'");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_file_logging_format_is_invalid()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "logging": {
                  "file": {
                    "format": "CompactJn"
                  }
                }
              }
            }
            """;

        AssertInvalidConfig(json, "Format 'CompactJn' is not supported");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_file_logging_rolling_interval_is_invalid()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "logging": {
                  "file": {
                    "rollingInterval": "Dy"
                  }
                }
              }
            }
            """;

        AssertInvalidConfig(json, "RollingInterval 'Dy' is not supported");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_file_logging_minimum_level_is_invalid()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "logging": {
                  "file": {
                    "minimumLevel": "Info"
                  }
                }
              }
            }
            """;

        AssertInvalidConfig(json, "MinimumLevel 'Info' is not supported");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_file_logging_retention_days_is_negative()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "logging": {
                  "file": {
                    "retentionDays": -1
                  }
                }
              }
            }
            """;

        AssertInvalidConfig(json, "RetentionDays must be greater than or equal to zero");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_file_logging_retained_file_count_limit_is_negative()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "logging": {
                  "file": {
                    "retainedFileCountLimit": -1
                  }
                }
              }
            }
            """;

        AssertInvalidConfig(json, "RetainedFileCountLimit must be greater than or equal to zero");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_seq_logging_server_url_is_invalid()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "logging": {
                  "seq": {
                    "serverUrl": "seq.local:5341"
                  }
                }
              }
            }
            """;

        AssertInvalidConfig(json, "ServerUrl must be an absolute HTTP or HTTPS URL");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_seq_logging_minimum_level_is_invalid()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "logging": {
                  "seq": {
                    "minimumLevel": "Debg"
                  }
                }
              }
            }
            """;

        AssertInvalidConfig(json, "MinimumLevel 'Debg' is not supported");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_video_retention_days_is_negative()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "video": {
                  "retentionDays": -1
                }
              }
            }
            """;

        AssertInvalidConfig(json, "RetentionDays must be greater than or equal to zero");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_video_max_total_size_bytes_is_less_than_one()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "video": {
                  "maxTotalSizeBytes": 0
                }
              }
            }
            """;

        AssertInvalidConfig(json, "MaxTotalSizeBytes must be greater than or equal to one");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_live_video_playback_buffer_milliseconds_is_negative()
    {
        string json = """
            {
              "version": 1,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": -1,
                  "maxPlaybackBufferMilliseconds": 100,
                  "decoderLowLatency": true,
                  "udpReceiveBufferBytes": 2097152
                }
              }
            }
            """;

        AssertInvalidConfig(json, "PlaybackBufferMilliseconds must be greater than or equal to zero");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_live_video_max_buffer_milliseconds_is_not_greater_than_playback_buffer_milliseconds()
    {
        string json = """
            {
              "version": 1,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": 50,
                  "maxPlaybackBufferMilliseconds": 50,
                  "decoderLowLatency": true,
                  "udpReceiveBufferBytes": 2097152
                }
              }
            }
            """;

        AssertInvalidConfig(json, "MaxPlaybackBufferMilliseconds must be greater than PlaybackBufferMilliseconds");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_live_video_max_buffer_milliseconds_is_too_large()
    {
        string json = """
            {
              "version": 1,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": 25,
                  "maxPlaybackBufferMilliseconds": 1001,
                  "decoderLowLatency": true,
                  "udpReceiveBufferBytes": 2097152
                }
              }
            }
            """;

        AssertInvalidConfig(json, "MaxPlaybackBufferMilliseconds must be less than or equal to 1000");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_live_video_udp_receive_buffer_bytes_is_too_small()
    {
        string json = """
            {
              "version": 1,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": 25,
                  "maxPlaybackBufferMilliseconds": 100,
                  "decoderLowLatency": true,
                  "udpReceiveBufferBytes": 262144
                }
              }
            }
            """;

        AssertInvalidConfig(json, "UdpReceiveBufferBytes must be greater than or equal to 524288");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_runtime_metrics_report_interval_seconds_is_less_than_one()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "runtimeMetrics": {
                  "reportIntervalSeconds": 0
                }
              }
            }
            """;

        AssertInvalidConfig(json, "ReportIntervalSeconds must be greater than or equal to one");
    }

    [Fact]
    public void DeserializeAndValidate_throws_when_json_contains_unknown_property()
    {
        string json = """
            {
              "version": 1,
              "diagnostics": {
                "logging": {
                  "file": {
                    "formt": "CompactJson"
                  }
                }
              }
            }
            """;

        JsonException ex = Assert.Throws<JsonException>(
            () => RecorderSettingsJson.DeserializeAndValidate(AddDefaultRequiredSections(json)));

        Assert.Contains("formt", ex.Message);
    }

    private static void AssertInvalidConfig(string json, string expectedMessage)
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => RecorderSettingsJson.DeserializeAndValidate(AddDefaultRequiredSections(json)));

        Assert.Contains(expectedMessage, ex.Message);
    }

    private static void AssertInvalidJson(string json, string expectedMessage)
    {
        JsonException ex = Assert.Throws<JsonException>(
            () => RecorderSettingsJson.DeserializeAndValidate(json));

        Assert.Contains(expectedMessage, ex.Message);
    }

    private static string AddDefaultRequiredSections(string json)
    {
        return AddDefaultDiagnostics(AddDefaultViewer(json));
    }

    private static string AddDefaultViewer(string json)
    {
        if (json.Contains("\"viewer\"", StringComparison.Ordinal))
        {
            return json;
        }

        return json.Replace(
            "\"version\": 1,",
            """
            "version": 1,
              "viewer": {
                "liveVideo": {
                  "playbackBufferMilliseconds": 25,
                  "maxPlaybackBufferMilliseconds": 100,
                  "decoderLowLatency": true,
                  "udpReceiveBufferBytes": 2097152
                }
              },
            """,
            StringComparison.Ordinal);
    }

    private static string AddDefaultDiagnostics(string json)
    {
        if (json.Contains("\"diagnostics\"", StringComparison.Ordinal))
        {
            return json;
        }

        return json.Replace(
            "\"version\": 1,",
            """
            "version": 1,
              "diagnostics": {
              },
            """,
            StringComparison.Ordinal);
    }
}
