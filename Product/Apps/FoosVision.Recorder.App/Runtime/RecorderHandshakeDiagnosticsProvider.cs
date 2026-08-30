// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Protocol.Messages.Handshake;
using FoosVision.Settings.Diagnostics;

namespace FoosVision.Recorder.App.Runtime;

public class RecorderHandshakeDiagnosticsProvider : IHandshakeDiagnosticsProvider
{
    public HandshakeDiagnosticsSettings GetDiagnosticsSettings()
    {
        DiagnosticsSeqLoggingSettings? seq = RecorderLoggingBootstrap.CurrentSettings?.Settings.Diagnostics.Logging.Seq;
        DiagnosticsRuntimeMetricsSettings? runtimeMetrics = RecorderLoggingBootstrap.CurrentSettings?.Settings.Diagnostics.RuntimeMetrics;

        if (seq is null && runtimeMetrics is null)
        {
            return new HandshakeDiagnosticsSettings();
        }

        return new HandshakeDiagnosticsSettings
        {
            Seq = seq is null
                ? new HandshakeSeqLoggingSettings()
                : new HandshakeSeqLoggingSettings
                {
                    Enabled = seq.Enabled,
                    ServerUrl = seq.ServerUrl,
                    MinimumLevel = seq.MinimumLevel,
                    SendTestEventOnStartup = seq.SendTestEventOnStartup,
                },
            RuntimeMetrics = runtimeMetrics is null
                ? new HandshakeRuntimeMetricsSettings()
                : new HandshakeRuntimeMetricsSettings
                {
                    Enabled = runtimeMetrics.Enabled,
                    ReportIntervalSeconds = runtimeMetrics.ReportIntervalSeconds,
                },
        };
    }
}
