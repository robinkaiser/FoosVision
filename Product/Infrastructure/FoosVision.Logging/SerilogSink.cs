// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using Serilog;

namespace FoosVision.Logging;

public class SerilogSink : ILoggerSink
{
    private readonly ILogger _Log;
    private readonly Func<string, Severity, bool>? _Filter;

    public SerilogSink(ILogger log, Func<string, Severity, bool>? filter = null)
    {
        _Log = log;
        _Filter = filter;
    }

    public void Emit(in LogEntry entry)
    {
        if (_Filter != null && !_Filter(entry.Source, entry.Severity))
        {
            return;
        }

        var log = _Log.ForContext("Source", entry.Source);
        var args = entry.Args;
        var level = SerilogLevel.Map(entry.Severity);

        if (args.Length == 0)
        {
            log.Write(level, entry.MessageTemplate);
            return;
        }

        if (args[0] is Exception exception)
        {
            object?[] structuredArgs = args.Length == 1 ? [] : args[1..];
            log.Write(level, exception, entry.MessageTemplate, structuredArgs);
            return;
        }

        log.Write(level, entry.MessageTemplate, args);
    }

    public void Dispose()
    {
        // Usually no-op; Serilog lifecycle is managed elsewhere.
    }
}
