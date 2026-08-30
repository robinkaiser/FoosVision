// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json;
using FoosVision.Settings;

namespace FoosVision.Recorder.App.Runtime;

public class RecorderConfigEditor
{
    private readonly ISettingsFileStore _FileStore;

    public RecorderConfigEditor(ISettingsFileStore fileStore)
    {
        _FileStore = fileStore;
    }

    public string LoadText()
    {
        return _FileStore.ReadAllText(GetConfigPath());
    }

    public RecorderConfigSaveResult SaveText(string text)
    {
        string configPath;

        try
        {
            configPath = GetConfigPath();
        }
        catch (InvalidOperationException ex)
        {
            return RecorderConfigSaveResult.Failed(ex.Message);
        }

        try
        {
            RecorderSettingsJson.DeserializeAndValidate(text);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return RecorderConfigSaveResult.Invalid(ex.Message);
        }

        try
        {
            _FileStore.WriteAllText(configPath, text);
            return RecorderConfigSaveResult.Saved();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return RecorderConfigSaveResult.Failed(ex.Message);
        }
    }

    private static string GetConfigPath()
    {
        return RecorderLoggingBootstrap.CurrentSettings?.Paths.Config
            ?? throw new InvalidOperationException("Recorder settings are not initialized.");
    }
}
