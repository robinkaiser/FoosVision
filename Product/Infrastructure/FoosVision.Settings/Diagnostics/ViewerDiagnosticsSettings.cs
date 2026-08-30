// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class ViewerDiagnosticsSettings
{
    public DiagnosticsLoggingSettings Logging { get; set; } = DiagnosticsLoggingSettings.CreateDefault();

    public DiagnosticsRuntimeMetricsSettings RuntimeMetrics { get; set; } = DiagnosticsRuntimeMetricsSettings.CreateDefault();

    public static ViewerDiagnosticsSettings CreateDefault()
    {
        return new ViewerDiagnosticsSettings();
    }

    public void Validate()
    {
        Logging ??= DiagnosticsLoggingSettings.CreateDefault();
        RuntimeMetrics ??= DiagnosticsRuntimeMetricsSettings.CreateDefault();

        Logging.Validate();
        RuntimeMetrics.Validate();
    }
}
