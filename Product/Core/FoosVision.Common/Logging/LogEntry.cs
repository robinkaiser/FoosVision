// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Logging;

public readonly record struct LogEntry
{
    public DateTimeOffset TimestampUtc { get; init; }

    public Severity Severity { get; init; }

    public string Source { get; init; }

    public string MessageTemplate { get; init; }

    public object?[] Args { get; init; }

    public static LogEntry Create(
        DateTimeOffset timestampUtc,
        Severity severity,
        string source,
        string messageTemplate,
        object?[] args)
    {
        return new LogEntry
        {
            TimestampUtc = timestampUtc,
            Severity = severity,
            Source = source,
            MessageTemplate = messageTemplate,
            Args = args,
        };
    }
}
