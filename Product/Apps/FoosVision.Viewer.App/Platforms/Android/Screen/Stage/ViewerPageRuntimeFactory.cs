// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Viewer.App.Platforms.Android.Screen.Page;
using FoosVision.Viewer.App.Screen.Page;
using FoosVision.Viewer.App.Screen.Stage;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Stage;

public class ViewerPageRuntimeFactory : IViewerPageRuntimeFactory
{
    public IViewerPageRuntime Create(ViewerPageViewModel viewModel)
    {
        return new PageRuntimeHost(viewModel);
    }
}
