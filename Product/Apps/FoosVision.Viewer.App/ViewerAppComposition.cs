// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Viewer.App.Screen.Page;

namespace FoosVision.Viewer.App;

/// <summary>
/// Exposes viewer UI composition for application hosts.
/// </summary>
public static partial class ViewerAppComposition
{
    public static MainPage CreateViewerPage()
    {
        return new MainPage(CreateViewerPageHost());
    }

    public static ViewerPageHost CreateViewerPageHost()
    {
        return ViewerPlatformComposition.CreateViewerPageHost();
    }

    public static IMauiHandlersCollection RegisterViewerHandlers(IMauiHandlersCollection handlers)
    {
        RegisterViewerPlatformHandlers(handlers);
        return handlers;
    }

    static partial void RegisterViewerPlatformHandlers(IMauiHandlersCollection handlers);
}
