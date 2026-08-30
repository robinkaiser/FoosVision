// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Metrics;

public class RuntimeMetricsOptions
{
    private static readonly TimeSpan _DefaultReportInterval = TimeSpan.FromSeconds(10);

    public bool Enabled { get; set; }

    public TimeSpan ReportInterval { get; set; } = _DefaultReportInterval;

    public string NamePrefix { get; set; } = string.Empty;

    public static RuntimeMetricsOptions CreateDefault()
    {
        return new RuntimeMetricsOptions();
    }

    public TimeSpan GetReportInterval()
    {
        if (ReportInterval > TimeSpan.Zero)
        {
            return ReportInterval;
        }

        return _DefaultReportInterval;
    }

    public string CreateMetricName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (string.IsNullOrWhiteSpace(NamePrefix))
        {
            return name;
        }

        return $"{NamePrefix}.{name}";
    }
}
