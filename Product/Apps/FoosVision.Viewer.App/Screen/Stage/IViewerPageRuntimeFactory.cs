// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Viewer.App.Screen.Page;

namespace FoosVision.Viewer.App.Screen.Stage;

public interface IViewerPageRuntimeFactory
{
    IViewerPageRuntime Create(ViewerPageViewModel viewModel);
}
