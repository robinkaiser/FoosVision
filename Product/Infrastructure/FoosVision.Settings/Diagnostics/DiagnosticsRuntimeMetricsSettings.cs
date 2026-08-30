// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class DiagnosticsRuntimeMetricsSettings
{
    public bool Enabled { get; set; }

    public int ReportIntervalSeconds { get; set; } = 10;

    public static DiagnosticsRuntimeMetricsSettings CreateDefault()
    {
        return new DiagnosticsRuntimeMetricsSettings();
    }

    public void Validate()
    {
        if (ReportIntervalSeconds < 1)
        {
            throw new InvalidOperationException($"{nameof(ReportIntervalSeconds)} must be greater than or equal to one.");
        }
    }
}
