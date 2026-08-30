// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Viewer.App.Screen.Stage;
using MauiPage = Microsoft.Maui.Controls.Page;

namespace FoosVision.Viewer.App.Screen.Page;

public class ViewerPageHost : IDisposable
{
    private readonly IViewerPageRuntime _PageRuntime;
    private bool _Disposed;

    public View StageContent { get; }

    public ViewerPageViewModel ViewModel { get; }

    public ViewerPageHost(IViewerPageRuntimeFactory pageRuntimeFactory)
    {
        ViewModel = new ViewerPageViewModel();
        _PageRuntime = pageRuntimeFactory.Create(ViewModel);
        StageContent = _PageRuntime.StageContent;
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        _PageRuntime.Dispose();
        ViewModel.Dispose();
        GC.SuppressFinalize(this);
    }

    public void OnAppearing()
    {
        _PageRuntime.OnAppearing();
    }

    public void OnDisappearing(IReadOnlyList<MauiPage> navigationStack, MauiPage page)
    {
        if (_Disposed)
        {
            return;
        }

        _PageRuntime.OnDisappearing();

        if (!navigationStack.Contains(page))
        {
            Dispose();
        }
    }
}
