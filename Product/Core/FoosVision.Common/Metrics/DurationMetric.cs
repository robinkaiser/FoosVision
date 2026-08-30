// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using FoosVision.Common.Logging;

namespace FoosVision.Common.Metrics;

public class DurationMetric
{
    private readonly MetricSampleAccumulator _Accumulator;
    private readonly Source _Log;
    private readonly string _Name;
    private readonly Func<long> _TimestampProvider;
    private readonly long _TimestampFrequency;
    private readonly long _ReportIntervalTicks;

    private long? _NextReportTimestamp;

    public DurationMetric(
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

    public void RecordElapsed(long startedTimestamp)
    {
        long now = _TimestampProvider();
        long elapsedTicks = Math.Max(0, now - startedTimestamp);
        double elapsedMs = elapsedTicks * 1000D / _TimestampFrequency;

        _Accumulator.Record(elapsedMs);
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

        if (snapshot.Count > 0)
        {
            _Log.Information(
                "{0}: count={1}, avg={2:0.###}ms, min={3:0.###}ms, max={4:0.###}ms, buckets={5}",
                _Name,
                snapshot.Count,
                snapshot.AverageMs,
                snapshot.MinMs,
                snapshot.MaxMs,
                snapshot.Buckets);
        }

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
