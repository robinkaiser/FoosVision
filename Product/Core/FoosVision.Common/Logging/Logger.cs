// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Logging;

public static class Logger
{
    private static readonly ILoggerSink _Null = new NullSink();
    private static ILoggerSink _Sink = _Null;
    private static Func<DateTimeOffset> _UtcNow = () => DateTimeOffset.UtcNow;

    public static void Bind(ILoggerSink sink)
    {
        _Sink = sink ?? _Null;
    }

    public static void UseClock(Func<DateTimeOffset> utcNow)
    {
        _UtcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    internal static void Write(
        string source,
        Severity severity,
        string messageTemplate,
        object?[] args)
    {
        if (severity < LogControl.MinimumSeverity)
        {
            return;
        }

        var entry = LogEntry.Create(_UtcNow(), severity, source, messageTemplate, args);

        _Sink.Emit(in entry);
    }
}
