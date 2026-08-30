// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Recorder.App.Runtime;
using FoosVision.Settings;

namespace FoosVision.Recorder.App;

/// <summary>
/// Creates recorder application components through explicit platform composition.
/// </summary>
public static partial class RecorderPlatformComposition
{
    public static MainPage CreateMainPage()
    {
        RecorderConfigEditorViewModel configEditor = new(new RecorderConfigEditor(new SettingsFileStore()));
        RecorderAboutViewModel about = new();
        MainViewModel viewModel = new(configEditor, about);
        IRecorderRuntimeFactory runtimeFactory = CreateRecorderRuntimeFactory(viewModel);
        return new MainPage(viewModel, runtimeFactory.Create());
    }

    /// <summary>
    /// Creates the platform-specific recorder runtime factory.
    /// </summary>
    /// <param name="viewModel">View model receiving recorder status updates.</param>
    /// <returns>Recorder runtime factory for the current platform.</returns>
    public static partial IRecorderRuntimeFactory CreateRecorderRuntimeFactory(MainViewModel viewModel);
}
