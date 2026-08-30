// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using FoosVision.Common.Logging;

namespace FoosVision.Common.Metrics;

public class CounterMetric
{
    private readonly Source _Log;
    private readonly string _Name;
    private readonly Func<long> _TimestampProvider;
    private readonly long _TimestampFrequency;
    private readonly long _ReportIntervalTicks;

    private long? _NextReportTimestamp;
    private long _PeriodStartedTimestamp;
    private long _Count;

    public CounterMetric(
        string name,
        Source log,
        TimeSpan reportInterval,
        Func<long>? timestampProvider = null,
        long? timestampFrequency = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(reportInterval, TimeSpan.Zero);

        _Name = name;
        _Log = log;
        _TimestampProvider = timestampProvider ?? Stopwatch.GetTimestamp;
        _TimestampFrequency = timestampFrequency ?? Stopwatch.Frequency;
        _ReportIntervalTicks = ToTimestampTicks(reportInterval);
    }

    public void Increment(long count = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        long now = _TimestampProvider();

        if (_NextReportTimestamp is null)
        {
            _PeriodStartedTimestamp = now;
            _NextReportTimestamp = now + _ReportIntervalTicks;
        }

        _Count += count;
        ReportIfDue(now);
    }

    private void ReportIfDue(long now)
    {
        long nextReportTimestamp = _NextReportTimestamp ??= now + _ReportIntervalTicks;

        if (now < nextReportTimestamp)
        {
            return;
        }

        double periodSeconds = Math.Max(0.001D, (now - _PeriodStartedTimestamp) / (double)_TimestampFrequency);

        _Log.Information(
            "{0}: count={1}, rate={2:0.###}/s",
            _Name,
            _Count,
            _Count / periodSeconds);

        _Count = 0;
        _PeriodStartedTimestamp = now;

        do
        {
            _NextReportTimestamp += _ReportIntervalTicks;
        }
        while (now >= _NextReportTimestamp.Value);
    }

    private long ToTimestampTicks(TimeSpan duration)
    {
        return Math.Max(1, (long)Math.Ceiling(duration.TotalSeconds * _TimestampFrequency));
    }
}
