// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Settings.Diagnostics;

namespace FoosVision.Settings;

public class ViewerSettingsInitializer
{
    private const string _DiagnosticsDirectoryName = "Diagnostics";
    private const string _LogsDirectoryName = "Logs";
    private const string _VideosDirectoryName = "Videos";
    private const string _ConfigFileName = "Config.json";
    private const string _ExampleConfigFileName = "Config.example.json";

    private readonly ISettingsFileStore _FileStore;

    public ViewerSettingsInitializer(ISettingsFileStore fileStore)
    {
        _FileStore = fileStore;
    }

    public ViewerSettingsContext Initialize(string? preferredAppFilesPath, string fallbackAppDataPath)
    {
        SettingsPaths paths = ResolvePaths(preferredAppFilesPath, fallbackAppDataPath);
        _FileStore.CreateDirectory(paths.Diagnostics.Logs);

        return new ViewerSettingsContext(paths, ViewerSettings.CreateDefault());
    }

    private SettingsPaths ResolvePaths(string? preferredAppFilesPath, string fallbackAppDataPath)
    {
        if (!string.IsNullOrWhiteSpace(preferredAppFilesPath))
        {
            if (_FileStore.CanWriteDirectory(preferredAppFilesPath))
            {
                return CreatePaths(preferredAppFilesPath);
            }
        }

        if (_FileStore.CanWriteDirectory(fallbackAppDataPath))
        {
            return CreatePaths(fallbackAppDataPath);
        }

        throw new InvalidOperationException("No writable settings directory is available.");
    }

    private static SettingsPaths CreatePaths(string rootPath)
    {
        string diagnosticsPath = Path.Combine(rootPath, _DiagnosticsDirectoryName);

        return new SettingsPaths(
            rootPath,
            Path.Combine(rootPath, _ConfigFileName),
            Path.Combine(rootPath, _ExampleConfigFileName),
            new DiagnosticsPaths(
                diagnosticsPath,
                Path.Combine(diagnosticsPath, _LogsDirectoryName),
                Path.Combine(diagnosticsPath, _VideosDirectoryName)));
    }
}
