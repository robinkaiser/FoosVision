// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Recorder.App.Runtime;
using System.ComponentModel;

namespace FoosVision.Recorder.App;

/// <summary>
/// Main MAUI host page for the recorder screen.
/// </summary>
public partial class MainPage : ContentPage
{
    private static readonly Source _Log = new("Recorder.MainPage");
    private readonly IRecorderRuntime _Runtime;
    private readonly MainViewModel _ViewModel;

    public MainPage(
        MainViewModel viewModel,
        IRecorderRuntime runtime)
    {
        _ViewModel = viewModel;
        _Runtime = runtime;
        BindingContext = viewModel;

        InitializeComponent();
        _ViewModel.About.PropertyChanged += OnAboutPropertyChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            await _Runtime.StartAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _Log.Error("Recorder page startup failed.", ex);
            _ViewModel.ShowFault("Startup failed");
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();

        await StopAsync(CancellationToken.None);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _Runtime.StopAsync(ct);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            _ViewModel.About.PropertyChanged -= OnAboutPropertyChanged;
            _Runtime.Dispose();
        }
    }

    private void OnAboutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RecorderAboutViewModel.IsVisible) or nameof(RecorderAboutViewModel.AboutText))
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await AboutScrollView.ScrollToAsync(0, 0, false);
            });
        }
    }
}
