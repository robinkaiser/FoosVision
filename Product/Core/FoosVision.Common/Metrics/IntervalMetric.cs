// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using FoosVision.Common.Logging;

namespace FoosVision.Common.Metrics;

public class IntervalMetric
{
    private readonly MetricSampleAccumulator _Accumulator;
    private readonly Source _Log;
    private readonly string _Name;
    private readonly Func<long> _TimestampProvider;
    private readonly long _TimestampFrequency;
    private readonly long _ReportIntervalTicks;

    private long? _NextReportTimestamp;
    private long? _LastEventTimestamp;
    private long? _LastEventTimestampFrequency;
    private long _PeriodStartedTimestamp;
    private long _EventCount;

    public IntervalMetric(
        string name,
        Source log,
        TimeSpan reportInterval,
        IReadOnlyList<double>? bucketUpperBoundsMs = null,
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
        _Accumulator = new MetricSampleAccumulator(bucketUpperBoundsMs);
    }

    public void Record()
    {
        Record(_TimestampProvider(), _TimestampFrequency);
    }

    public void Record(long eventTimestamp, long eventTimestampFrequency)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(eventTimestampFrequency, 0);

        long now = _TimestampProvider();

        if (_NextReportTimestamp is null)
        {
            _PeriodStartedTimestamp = now;
            _NextReportTimestamp = now + _ReportIntervalTicks;
        }

        _EventCount++;

        if (_LastEventTimestamp is not null && _LastEventTimestampFrequency == eventTimestampFrequency)
        {
            long intervalTicks = Math.Max(0, eventTimestamp - _LastEventTimestamp.Value);
            double intervalMs = intervalTicks * 1000D / eventTimestampFrequency;
            _Accumulator.Record(intervalMs);
        }

        _LastEventTimestamp = eventTimestamp;
        _LastEventTimestampFrequency = eventTimestampFrequency;

        ReportIfDue(now);
    }

    private void ReportIfDue(long now)
    {
        long nextReportTimestamp = _NextReportTimestamp ??= now + _ReportIntervalTicks;

        if (now < nextReportTimestamp)
        {
            return;
        }

        MetricSampleSnapshot snapshot = _Accumulator.SnapshotAndReset();
        double periodSeconds = Math.Max(0.001D, (now - _PeriodStartedTimestamp) / (double)_TimestampFrequency);

        _Log.Information(
            "{0}: events={1}, rate={2:0.###}/s, intervals={3}, avg={4:0.###}ms, min={5:0.###}ms, max={6:0.###}ms, buckets={7}",
            _Name,
            _EventCount,
            _EventCount / periodSeconds,
            snapshot.Count,
            snapshot.AverageMs,
            snapshot.MinMs,
            snapshot.MaxMs,
            snapshot.Buckets);

        _EventCount = 0;
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
