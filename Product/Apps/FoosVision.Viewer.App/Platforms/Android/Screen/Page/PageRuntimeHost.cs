// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Viewer.App.Screen.Page;
using FoosVision.Viewer.App.Screen.Stage;

namespace FoosVision.Viewer.App.Platforms.Android.Screen.Page;

public class PageRuntimeHost :
    IViewerPageRuntime,
    IDisposable
{
    private readonly OrientationListener _OrientationListener;
    private readonly ViewerWifiMulticastLock _WifiMulticastLock;

    public PageRuntimeHost(ViewerPageViewModel viewModel)
    {
        _WifiMulticastLock = ViewerWifiMulticastLock.Acquire(Platform.AppContext);
        StageView = new StageView
        {
            RuntimeAttachedAsync = viewModel.AttachRuntimeAsync,
        };
        _OrientationListener = new OrientationListener(Platform.CurrentActivity!, viewModel.UpdateControlsRotation);
    }

    public View StageContent => StageView;

    public StageView StageView { get; }

    public void Dispose()
    {
        StageView.RuntimeAttachedAsync = null;
        _OrientationListener.Disable();
        _WifiMulticastLock.Dispose();
        StageView.Handler?.DisconnectHandler();
        GC.SuppressFinalize(this);
    }

    public void OnAppearing()
    {
        _OrientationListener.Enable();
        _OrientationListener.ApplyInitialRotation();
    }

    public void OnDisappearing()
    {
        _OrientationListener.Disable();
    }
}
