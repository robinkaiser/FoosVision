// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Common.Logging;
using FoosVision.Logging;
using FoosVision.Settings;
using FoosVision.Settings.Diagnostics;
using Serilog;
using Serilog.Debugging;

namespace FoosVision.Viewer.App.Runtime;

public static class ViewerLoggingBootstrap
{
    public static ViewerSettingsContext? CurrentSettings { get; private set; }

    public static void Initialize(string? externalAppFilesDirectory, string appDataDirectory)
    {
        SelfLog.Enable(message => System.Diagnostics.Debug.WriteLine($"[Serilog.SelfLog] {message}"));

        ViewerSettingsInitializer settingsInitializer = new(new SettingsFileStore());
        CurrentSettings = settingsInitializer.Initialize(externalAppFilesDirectory, appDataDirectory);

        Log.Logger = SerilogLoggerFactory.CreateLogger(
            "Viewer",
            AppInfo.Current.VersionString,
            CurrentSettings.Paths.Diagnostics,
            CurrentSettings.Settings.Diagnostics.Logging);

        Logger.Bind(new SerilogSink(Log.Logger));
        LogControl.MinimumSeverity = SerilogLoggerFactory.GetMinimumSeverity(CurrentSettings.Settings.Diagnostics.Logging);

        Log.Information(
            "Viewer settings initialized. SettingsPath={SettingsPath} DiagnosticsPath={DiagnosticsPath} Sinks={Sinks} MinimumSeverity={MinimumSeverity}",
            CurrentSettings.Paths.Root,
            CurrentSettings.Paths.Diagnostics.Root,
            SerilogLoggerFactory.DescribeSinks(CurrentSettings.Settings.Diagnostics.Logging),
            LogControl.MinimumSeverity);
    }

    public static void ApplyHandshakeDiagnostics(RecorderConnection connection)
    {
        if (CurrentSettings is null)
        {
            Log.Warning("Viewer diagnostics were not initialized before handshake diagnostics were applied.");
            return;
        }

        DiagnosticsLoggingSettings logging = DiagnosticsLoggingSettings.CreateDefault();
        logging.File = CurrentSettings.Settings.Diagnostics.Logging.File;
        logging.Seq = CreateSeqSettings(connection);
        DiagnosticsRuntimeMetricsSettings runtimeMetrics = CreateRuntimeMetricsSettings(connection);

        CurrentSettings = CurrentSettings with
        {
            Settings = new ViewerSettings
            {
                Diagnostics = new ViewerDiagnosticsSettings
                {
                    Logging = logging,
                    RuntimeMetrics = runtimeMetrics,
                },
            },
        };

        var previousLogger = Log.Logger;
        Log.Logger = SerilogLoggerFactory.CreateLogger(
            "Viewer",
            AppInfo.Current.VersionString,
            CurrentSettings.Paths.Diagnostics,
            CurrentSettings.Settings.Diagnostics.Logging);

        Logger.Bind(new SerilogSink(Log.Logger));
        LogControl.MinimumSeverity = SerilogLoggerFactory.GetMinimumSeverity(CurrentSettings.Settings.Diagnostics.Logging);
        (previousLogger as IDisposable)?.Dispose();

        Log.Information(
            "Viewer handshake diagnostics applied. RecorderIp={RecorderIp} DiagnosticsPath={DiagnosticsPath} Sinks={Sinks} MinimumSeverity={MinimumSeverity}",
            connection.RecorderIpAddress,
            CurrentSettings.Paths.Diagnostics.Root,
            SerilogLoggerFactory.DescribeSinks(CurrentSettings.Settings.Diagnostics.Logging),
            LogControl.MinimumSeverity);

        if (CurrentSettings.Settings.Diagnostics.Logging.Seq is { Enabled: true, SendTestEventOnStartup: true })
        {
            Log.Information("Viewer Seq handshake test event.");
        }
    }

    public static void Shutdown()
    {
        Log.CloseAndFlush();
    }

    private static DiagnosticsSeqLoggingSettings CreateSeqSettings(RecorderConnection connection)
    {
        var seq = connection.Diagnostics.Seq;

        return new DiagnosticsSeqLoggingSettings
        {
            Enabled = seq.Enabled && !string.IsNullOrWhiteSpace(seq.ServerUrl),
            ServerUrl = seq.ServerUrl,
            ApiKey = string.Empty,
            MinimumLevel = seq.MinimumLevel,
            SendTestEventOnStartup = seq.SendTestEventOnStartup,
        };
    }

    private static DiagnosticsRuntimeMetricsSettings CreateRuntimeMetricsSettings(RecorderConnection connection)
    {
        var runtimeMetrics = connection.Diagnostics.RuntimeMetrics;

        return new DiagnosticsRuntimeMetricsSettings
        {
            Enabled = runtimeMetrics.Enabled,
            ReportIntervalSeconds = runtimeMetrics.ReportIntervalSeconds,
        };
    }
}
