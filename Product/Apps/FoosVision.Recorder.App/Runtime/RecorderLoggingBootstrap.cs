// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Logging;
using FoosVision.Settings;
using Serilog;
using Serilog.Debugging;

namespace FoosVision.Recorder.App.Runtime;

public static class RecorderLoggingBootstrap
{
    public static RecorderSettingsContext? CurrentSettings { get; private set; }

    public static void Initialize(string? externalAppFilesDirectory, string appDataDirectory)
    {
        SelfLog.Enable(message => System.Diagnostics.Debug.WriteLine($"[Serilog.SelfLog] {message}"));

        RecorderSettingsInitializer settingsInitializer = new(new SettingsFileStore());
        CurrentSettings = settingsInitializer.Initialize(externalAppFilesDirectory, appDataDirectory);

        Log.Logger = SerilogLoggerFactory.CreateLogger(
            "Recorder",
            AppInfo.Current.VersionString,
            CurrentSettings.Paths.Diagnostics,
            CurrentSettings.Settings.Diagnostics.Logging);

        Logger.Bind(new SerilogSink(Log.Logger));
        LogControl.MinimumSeverity = SerilogLoggerFactory.GetMinimumSeverity(CurrentSettings.Settings.Diagnostics.Logging);

        Log.Information(
            "Recorder settings initialized. SettingsPath={SettingsPath} DiagnosticsPath={DiagnosticsPath} ConfigVersion={ConfigVersion} ConfigSource={ConfigSource} ConfigError={ConfigError} Sinks={Sinks} MinimumSeverity={MinimumSeverity}",
            CurrentSettings.Paths.Root,
            CurrentSettings.Paths.Diagnostics.Root,
            CurrentSettings.Settings.Version,
            CurrentSettings.ConfigSource,
            CurrentSettings.ConfigError,
            SerilogLoggerFactory.DescribeSinks(CurrentSettings.Settings.Diagnostics.Logging),
            LogControl.MinimumSeverity);

        if (CurrentSettings.Settings.Diagnostics.Logging.Seq is { Enabled: true, SendTestEventOnStartup: true })
        {
            Log.Information("Recorder Seq startup test event.");
        }
    }

    public static void Shutdown()
    {
        Log.CloseAndFlush();
    }
}
