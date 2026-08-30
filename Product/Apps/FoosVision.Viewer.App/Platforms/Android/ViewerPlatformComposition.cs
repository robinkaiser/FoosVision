// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Viewer.App.Platforms.Android.Screen.Stage;
using FoosVision.Viewer.App.Screen.Stage;

namespace FoosVision.Viewer.App;

/// <summary>
/// Provides Android-specific runtime factory wiring for the viewer app.
/// </summary>
public static partial class ViewerPlatformComposition
{
    public static IViewerPageRuntimeFactory CreateViewerPageRuntimeFactory()
    {
        return new ViewerPageRuntimeFactory();
    }
}
