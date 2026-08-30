// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Handshake;

namespace FoosVision.Adapters.Recorder.UnitTests.Connectivity;

public class HandshakeHandlerTests
{
    [Fact]
    public void Handle_returns_protocol_version_app_version_and_diagnostics()
    {
        TestVersionProvider versionProvider = new();
        TestDiagnosticsProvider diagnosticsProvider = new();
        TestViewerSettingsProvider viewerSettingsProvider = new();
        HandshakeHandler sut = new(
            versionProvider,
            diagnosticsProvider,
            viewerSettingsProvider,
            _ => true);

        HelloResponse result = sut.Handle(new HelloRequest());

        Assert.Equal(ProtocolVersions.Current, result.ProtocolVersion);
        Assert.Equal("1.2.3-recorder", result.RecorderAppVersion);
        Assert.True(result.Accepted);
        Assert.Equal(string.Empty, result.RejectionReason);
        Assert.True(result.Diagnostics.Seq.Enabled);
        Assert.Equal("http://seq.local:5341", result.Diagnostics.Seq.ServerUrl);
        Assert.Equal("Debug", result.Diagnostics.Seq.MinimumLevel);
        Assert.True(result.Diagnostics.Seq.SendTestEventOnStartup);
        Assert.True(result.Diagnostics.RuntimeMetrics.Enabled);
        Assert.Equal(7, result.Diagnostics.RuntimeMetrics.ReportIntervalSeconds);
        Assert.Equal(50, result.Viewer.LiveVideo.PlaybackBufferMilliseconds);
        Assert.Equal(200, result.Viewer.LiveVideo.MaxPlaybackBufferMilliseconds);
        Assert.False(result.Viewer.LiveVideo.DecoderLowLatency);
        Assert.Equal(1048576, result.Viewer.LiveVideo.UdpReceiveBufferBytes);
    }

    [Fact]
    public void Handle_returns_default_diagnostics_without_provider()
    {
        TestVersionProvider versionProvider = new();
        HandshakeHandler sut = new(
            versionProvider,
            new DefaultHandshakeDiagnosticsProvider(),
            new DefaultHandshakeViewerSettingsProvider(),
            _ => true);

        HelloResponse result = sut.Handle(new HelloRequest());

        Assert.False(result.Diagnostics.Seq.Enabled);
        Assert.Equal(string.Empty, result.Diagnostics.Seq.ServerUrl);
        Assert.False(result.Diagnostics.RuntimeMetrics.Enabled);
        Assert.Equal(10, result.Diagnostics.RuntimeMetrics.ReportIntervalSeconds);
        Assert.Equal(25, result.Viewer.LiveVideo.PlaybackBufferMilliseconds);
        Assert.Equal(100, result.Viewer.LiveVideo.MaxPlaybackBufferMilliseconds);
    }

    [Fact]
    public void Handle_returns_busy_response_when_viewer_is_not_accepted()
    {
        TestVersionProvider versionProvider = new();
        HandshakeHandler sut = new(
            versionProvider,
            new DefaultHandshakeDiagnosticsProvider(),
            new DefaultHandshakeViewerSettingsProvider(),
            _ => false);

        HelloResponse result = sut.Handle(new HelloRequest
        {
            ViewerIpAddress = "192.168.178.20",
        });

        Assert.False(result.Accepted);
        Assert.Equal("RecorderBusy", result.RejectionReason);
        Assert.Equal(ProtocolVersions.Current, result.ProtocolVersion);
        Assert.Equal("1.2.3-recorder", result.RecorderAppVersion);
    }

    private class TestVersionProvider : IRecorderVersionProvider
    {
        public string GetAppVersion()
        {
            return "1.2.3-recorder";
        }
    }

    private class TestDiagnosticsProvider : IHandshakeDiagnosticsProvider
    {
        public HandshakeDiagnosticsSettings GetDiagnosticsSettings()
        {
            return new HandshakeDiagnosticsSettings
            {
                Seq = new HandshakeSeqLoggingSettings
                {
                    Enabled = true,
                    ServerUrl = "http://seq.local:5341",
                    MinimumLevel = "Debug",
                    SendTestEventOnStartup = true,
                },
                RuntimeMetrics = new HandshakeRuntimeMetricsSettings
                {
                    Enabled = true,
                    ReportIntervalSeconds = 7,
                },
            };
        }
    }

    private class TestViewerSettingsProvider : IHandshakeViewerSettingsProvider
    {
        public HandshakeViewerSettings GetViewerSettings()
        {
            return new HandshakeViewerSettings
            {
                LiveVideo = new HandshakeViewerLiveVideoSettings
                {
                    PlaybackBufferMilliseconds = 50,
                    MaxPlaybackBufferMilliseconds = 200,
                    DecoderLowLatency = false,
                    UdpReceiveBufferBytes = 1048576,
                },
            };
        }
    }
}
