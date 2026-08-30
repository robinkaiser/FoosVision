// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Viewer.App.Screen.Page;
using FoosVision.Viewer.App.Screen.Stage;

namespace FoosVision.Viewer.App;

/// <summary>
/// Creates viewer application components through explicit platform composition.
/// </summary>
public static partial class ViewerPlatformComposition
{
    public static ViewerPageHost CreateViewerPageHost()
    {
        IViewerPageRuntimeFactory pageRuntimeFactory = CreateViewerPageRuntimeFactory();
        return new ViewerPageHost(pageRuntimeFactory);
    }
}
