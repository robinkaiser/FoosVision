// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json;
using FoosVision.Settings.Diagnostics;

namespace FoosVision.Settings;

public class RecorderSettingsInitializer
{
    private const string _DiagnosticsDirectoryName = "Diagnostics";
    private const string _LogsDirectoryName = "Logs";
    private const string _VideosDirectoryName = "Videos";
    private const string _ConfigFileName = "Config.json";
    private const string _ExampleConfigFileName = "Config.example.json";

    private readonly ISettingsFileStore _FileStore;

    public RecorderSettingsInitializer(ISettingsFileStore fileStore)
    {
        _FileStore = fileStore;
    }

    public RecorderSettingsContext Initialize(string? preferredAppFilesPath, string fallbackAppDataPath)
    {
        SettingsPaths paths = ResolvePaths(preferredAppFilesPath, fallbackAppDataPath);

        _FileStore.CreateDirectory(paths.Diagnostics.Logs);
        _FileStore.CreateDirectory(paths.Diagnostics.Videos);
        WriteExampleConfig(paths);
        bool createdConfigFromExample = WriteConfigFromExampleIfMissing(paths);

        return LoadConfig(paths, createdConfigFromExample);
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

    private void WriteExampleConfig(SettingsPaths paths)
    {
        string example = Serialize(RecorderSettings.CreateDefault());

        if (_FileStore.FileExists(paths.ExampleConfig) &&
            string.Equals(_FileStore.ReadAllText(paths.ExampleConfig), example, StringComparison.Ordinal))
        {
            return;
        }

        _FileStore.WriteAllText(paths.ExampleConfig, example);
    }

    private bool WriteConfigFromExampleIfMissing(SettingsPaths paths)
    {
        if (_FileStore.FileExists(paths.Config))
        {
            return false;
        }

        _FileStore.WriteAllText(paths.Config, _FileStore.ReadAllText(paths.ExampleConfig));
        return true;
    }

    private RecorderSettingsContext LoadConfig(SettingsPaths paths, bool createdConfigFromExample)
    {
        if (!_FileStore.FileExists(paths.Config) || createdConfigFromExample)
        {
            return new RecorderSettingsContext(
                paths,
                RecorderSettings.CreateDefault(),
                SettingsConfigSource.DefaultsMissingConfig,
                null);
        }

        try
        {
            RecorderSettings settings = RecorderSettingsJson.DeserializeAndValidate(_FileStore.ReadAllText(paths.Config));

            return new RecorderSettingsContext(paths, settings, SettingsConfigSource.ConfigFile, null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return new RecorderSettingsContext(
                paths,
                RecorderSettings.CreateDefault(),
                SettingsConfigSource.DefaultsInvalidConfig,
                ex.Message);
        }
    }

    private static string Serialize(RecorderSettings settings)
    {
        return RecorderSettingsJson.Serialize(settings);
    }
}
