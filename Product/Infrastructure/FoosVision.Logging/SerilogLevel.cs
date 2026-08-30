// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using Serilog.Events;

namespace FoosVision.Logging;

public static class SerilogLevel
{
    public static LogEventLevel Map(Severity s) => s switch
    {
        Severity.Verbose => LogEventLevel.Verbose,
        Severity.Debug => LogEventLevel.Debug,
        Severity.Information => LogEventLevel.Information,
        Severity.Warning => LogEventLevel.Warning,
        Severity.Error => LogEventLevel.Error,
        Severity.Fatal => LogEventLevel.Fatal,
        _ => LogEventLevel.Information,
    };
}
