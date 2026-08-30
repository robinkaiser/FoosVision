// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.ComponentModel;
using FoosVision.Viewer.App.Screen.Page;

namespace FoosVision.Viewer.App;

/// <summary>
/// Main MAUI host page for the viewer screen.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly ViewerPageHost _Host;

    public MainPage(ViewerPageHost host)
    {
        _Host = host;
        BindingContext = _Host.ViewModel;

        InitializeComponent();
        StageHost.Content = _Host.StageContent;
        _Host.ViewModel.About.PropertyChanged += OnAboutPropertyChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _Host.OnAppearing();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _Host.OnDisappearing(Navigation.NavigationStack, this);
    }

    public void Stop()
    {
        _Host.Dispose();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            _Host.ViewModel.About.PropertyChanged -= OnAboutPropertyChanged;
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        _Host.ViewModel.UpdateControlsPlacement(width, height);
    }

    private void OnAboutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewerAboutViewModel.IsVisible) or nameof(ViewerAboutViewModel.AboutText))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await AboutScrollView.ScrollToAsync(0, 0, false);
            });
        }
    }
}
