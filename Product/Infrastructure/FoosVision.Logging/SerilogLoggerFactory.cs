// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Settings.Diagnostics;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace FoosVision.Logging;

public static class SerilogLoggerFactory
{
    private const string _DefaultOutputTemplate = "{Timestamp:O} [{Level:u3}] {Message:lj}{NewLine}{Exception}";

    public static Serilog.Core.Logger CreateLogger(
        string appName,
        string appVersion,
        DiagnosticsPaths paths,
        DiagnosticsLoggingSettings settings)
    {
        LoggerConfiguration configuration = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.WithProperty("App", appName)
            .Enrich.WithProperty("AppVersion", appVersion)
            .Enrich.WithProperty("DiagnosticsPath", paths.Root);

        if (settings.File.Enabled)
        {
            AddFileSink(configuration, appName, paths, settings.File);
        }

        if (settings.Seq.Enabled)
        {
            AddSeqSink(configuration, settings.Seq);
        }

        return configuration.CreateLogger();
    }

    public static Severity GetMinimumSeverity(DiagnosticsLoggingSettings settings)
    {
        LogEventLevel minimumLevel = LogEventLevel.Fatal;

        if (settings.File.Enabled)
        {
            minimumLevel = Min(minimumLevel, ParseLogEventLevel(settings.File.MinimumLevel, LogEventLevel.Information));
        }

        if (settings.Seq.Enabled)
        {
            minimumLevel = Min(minimumLevel, ParseLogEventLevel(settings.Seq.MinimumLevel, LogEventLevel.Debug));
        }

        return ToSeverity(minimumLevel);
    }

    public static string DescribeSinks(DiagnosticsLoggingSettings settings)
    {
        List<string> sinks = [];

        if (settings.File.Enabled)
        {
            sinks.Add("File");
        }

        if (settings.Seq.Enabled)
        {
            sinks.Add("Seq");
        }

        return sinks.Count == 0 ? "None" : string.Join(",", sinks);
    }

    private static void AddFileSink(
        LoggerConfiguration configuration,
        string appName,
        DiagnosticsPaths paths,
        DiagnosticsFileLoggingSettings settings)
    {
        string filePath = Path.Combine(paths.Logs, $"{appName.ToLowerInvariant()}-.log");
        LogEventLevel minimumLevel = ParseLogEventLevel(settings.MinimumLevel, LogEventLevel.Information);
        RollingInterval rollingInterval = ParseRollingInterval(settings.RollingInterval);
        TimeSpan? retainedFileTimeLimit = settings.RetentionDays > 0
            ? TimeSpan.FromDays(settings.RetentionDays)
            : null;
        int? retainedFileCountLimit = settings.RetainedFileCountLimit > 0
            ? settings.RetainedFileCountLimit
            : null;

        if (string.Equals(settings.Format, "CompactJson", StringComparison.OrdinalIgnoreCase))
        {
            configuration.WriteTo.File(
                new CompactJsonFormatter(),
                filePath,
                restrictedToMinimumLevel: minimumLevel,
                rollingInterval: rollingInterval,
                retainedFileCountLimit: retainedFileCountLimit,
                retainedFileTimeLimit: retainedFileTimeLimit);
            return;
        }

        configuration.WriteTo.File(
            filePath,
            restrictedToMinimumLevel: minimumLevel,
            outputTemplate: _DefaultOutputTemplate,
            rollingInterval: rollingInterval,
            retainedFileCountLimit: retainedFileCountLimit,
            retainedFileTimeLimit: retainedFileTimeLimit);
    }

    private static void AddSeqSink(LoggerConfiguration configuration, DiagnosticsSeqLoggingSettings settings)
    {
        configuration.WriteTo.Seq(
            settings.ServerUrl,
            apiKey: null,
            restrictedToMinimumLevel: ParseLogEventLevel(settings.MinimumLevel, LogEventLevel.Debug),
            batchPostingLimit: 50,
            period: TimeSpan.FromMilliseconds(500));
    }

    private static LogEventLevel ParseLogEventLevel(string value, LogEventLevel fallback)
    {
        return Enum.TryParse(value, ignoreCase: true, out LogEventLevel level)
            ? level
            : fallback;
    }

    private static RollingInterval ParseRollingInterval(string value)
    {
        return Enum.TryParse(value, ignoreCase: true, out RollingInterval interval)
            ? interval
            : RollingInterval.Day;
    }

    private static LogEventLevel Min(LogEventLevel left, LogEventLevel right)
    {
        return left < right ? left : right;
    }

    private static Severity ToSeverity(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => Severity.Verbose,
            LogEventLevel.Debug => Severity.Debug,
            LogEventLevel.Information => Severity.Information,
            LogEventLevel.Warning => Severity.Warning,
            LogEventLevel.Error => Severity.Error,
            LogEventLevel.Fatal => Severity.Fatal,
            _ => Severity.Information,
        };
    }
}
