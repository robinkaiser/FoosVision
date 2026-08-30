// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class RecorderDiagnosticsSettings
{
    public DiagnosticsLoggingSettings Logging { get; set; } = DiagnosticsLoggingSettings.CreateDefault();

    public DiagnosticsVideoSettings Video { get; set; } = DiagnosticsVideoSettings.CreateDefault();

    public DiagnosticsRuntimeMetricsSettings RuntimeMetrics { get; set; } = DiagnosticsRuntimeMetricsSettings.CreateDefault();

    public DiagnosticsVisionSettings Vision { get; set; } = DiagnosticsVisionSettings.CreateDefault();

    public static RecorderDiagnosticsSettings CreateDefault()
    {
        return new RecorderDiagnosticsSettings();
    }

    public void Validate()
    {
        Logging ??= DiagnosticsLoggingSettings.CreateDefault();
        Video ??= DiagnosticsVideoSettings.CreateDefault();
        RuntimeMetrics ??= DiagnosticsRuntimeMetricsSettings.CreateDefault();
        Vision ??= DiagnosticsVisionSettings.CreateDefault();

        Logging.Validate();
        Video.Validate();
        RuntimeMetrics.Validate();
        Vision.Validate();
    }
}
