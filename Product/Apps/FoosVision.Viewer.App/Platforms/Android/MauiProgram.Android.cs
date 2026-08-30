// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Viewer.App.Platforms.Android.Screen.Stage;
using FoosVision.Viewer.App.Screen.Stage;

namespace FoosVision.Viewer.App;

/// <summary>
/// Supplies Android-specific MAUI handler registrations for the viewer library.
/// </summary>
public static partial class ViewerAppComposition
{
    static partial void RegisterViewerPlatformHandlers(IMauiHandlersCollection handlers)
    {
        handlers.AddHandler(typeof(StageView), typeof(StageViewHandler));
    }
}
