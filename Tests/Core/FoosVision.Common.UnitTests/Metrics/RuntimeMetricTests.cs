// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;

namespace FoosVision.Common.UnitTests.Metrics;

public class RuntimeMetricTests
{
    [Fact]
    public void DurationMetric_reports_samples_when_report_interval_elapsed()
    {
        RecordingSink sink = BindRecordingSink();
        long now = 0;
        DurationMetric metric = new(
            "copy",
            new Source("metrics"),
            TimeSpan.FromMilliseconds(10),
            [5D],
            () => now,
            1000);

        now = 4;
        metric.RecordElapsed(0);
        now = 14;
        metric.RecordElapsed(10);

        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal("copy", entry.Args[0]);
        Assert.Equal(2L, entry.Args[1]);
        Assert.Equal(4D, entry.Args[2]);
        Assert.Equal(4D, entry.Args[3]);
        Assert.Equal(4D, entry.Args[4]);
        Assert.Equal("<=5ms:2, >5ms:0", entry.Args[5]);
    }

    [Fact]
    public void IntervalMetric_reports_event_rate_and_intervals()
    {
        RecordingSink sink = BindRecordingSink();
        long now = 0;
        IntervalMetric metric = new(
            "callback",
            new Source("metrics"),
            TimeSpan.FromMilliseconds(10),
            [5D],
            () => now,
            1000);

        metric.Record(0, 1000);
        now = 5;
        metric.Record(5, 1000);
        now = 10;
        metric.Record(10, 1000);

        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal("callback", entry.Args[0]);
        Assert.Equal(3L, entry.Args[1]);
        Assert.Equal(300D, entry.Args[2]);
        Assert.Equal(2L, entry.Args[3]);
        Assert.Equal(5D, entry.Args[4]);
        Assert.Equal(5D, entry.Args[5]);
        Assert.Equal(5D, entry.Args[6]);
        Assert.Equal("<=5ms:2, >5ms:0", entry.Args[7]);
    }

    [Fact]
    public void CounterMetric_reports_count_and_rate()
    {
        RecordingSink sink = BindRecordingSink();
        long now = 0;
        CounterMetric metric = new(
            "buffer-misses",
            new Source("metrics"),
            TimeSpan.FromMilliseconds(10),
            () => now,
            1000);

        metric.Increment();
        now = 10;
        metric.Increment(2);

        LogEntry entry = Assert.Single(sink.Entries);
        Assert.Equal("buffer-misses", entry.Args[0]);
        Assert.Equal(3L, entry.Args[1]);
        Assert.Equal(300D, entry.Args[2]);
    }

    private static RecordingSink BindRecordingSink()
    {
        RecordingSink sink = new();
        Logger.Bind(sink);
        LogControl.MinimumSeverity = Severity.Information;
        return sink;
    }

    private class RecordingSink : ILoggerSink
    {
        public List<LogEntry> Entries { get; } = [];

        public void Emit(in LogEntry entry)
        {
            Entries.Add(entry);
        }

        public void Dispose()
        {
        }
    }
}
