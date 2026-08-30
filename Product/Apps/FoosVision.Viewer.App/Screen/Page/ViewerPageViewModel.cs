// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.ComponentModel;
using System.Runtime.CompilerServices;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Viewer.App.Screen.Controls;
using FoosVision.Viewer.App.Screen.Stage;

namespace FoosVision.Viewer.App.Screen.Page;

public class ViewerPageViewModel :
    INotifyPropertyChanged,
    IDisposable
{
    private const double _ReferenceAspectRatio = 16.0 / 9.0;
    private readonly ViewerSessionController _SessionController;
    private IViewerScreenRuntime? _ScreenRuntime;
    private ViewerControlsPlacement _ControlsPlacement = ViewerControlsPlacement.Bottom;
    private bool _Disposed;

    public ViewerPageViewModel()
    {
        _SessionController = new ViewerSessionController();
        About = new ViewerAboutViewModel();
        ControlsViewModel = new ViewerControlsViewModel(this, _SessionController);
        _SessionController.UiStateChanged += OnUiStateChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<float>? ControlsRotationChanged;

    public event Action<double?>? StreamFpsChanged;

    public ViewerControlsViewModel ControlsViewModel { get; }

    public ViewerAboutViewModel About { get; }

    public ViewerControlsPlacement ControlsPlacement => _ControlsPlacement;

    public bool IsBottomControlsVisible => _ControlsPlacement == ViewerControlsPlacement.Bottom;

    public bool IsSideControlsVisible => _ControlsPlacement == ViewerControlsPlacement.Side;

    public bool IsBottomLandscapeControlsVisible => IsBottomControlsVisible && !IsPortraitControlsRotation;

    public bool IsBottomPortraitControlsVisible => IsBottomControlsVisible && IsPortraitControlsRotation;

    public bool IsSideLandscapeControlsVisible => IsSideControlsVisible && !IsPortraitControlsRotation;

    public bool IsSidePortraitControlsVisible => IsSideControlsVisible && IsPortraitControlsRotation;

    public float ControlsRotationDegrees { get; private set; }

    public double? StreamFps { get; private set; }

    public Task AttachRuntimeAsync(IViewerScreenRuntime runtime)
    {
        _ScreenRuntime?.StreamFpsChanged -= OnStreamFpsChanged;

        _ScreenRuntime = runtime;
        runtime.StreamFpsChanged += OnStreamFpsChanged;
        runtime.UpdateSessionUiState(_SessionController.CurrentUiState);
        runtime.UpdateOverlayRotation(ControlsRotationDegrees);
        return _SessionController.AttachRuntimeAsync(runtime);
    }

    public void UpdateControlsRotation(float rotationDegrees)
    {
        bool wasPortraitControlsRotation = IsPortraitControlsRotation;
        if (Math.Abs(ControlsRotationDegrees - rotationDegrees) < 0.1f)
        {
            return;
        }

        ControlsRotationDegrees = rotationDegrees;
        _ScreenRuntime?.UpdateOverlayRotation(rotationDegrees);
        OnPropertyChanged(nameof(ControlsRotationDegrees));
        ControlsRotationChanged?.Invoke(rotationDegrees);

        if (wasPortraitControlsRotation != IsPortraitControlsRotation)
        {
            OnControlsVisibilityChanged();
        }
    }

    public void UpdateControlsPlacement(double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        double availableAspectRatio = width / height;
        ViewerControlsPlacement controlsPlacement = availableAspectRatio >= _ReferenceAspectRatio
            ? ViewerControlsPlacement.Side
            : ViewerControlsPlacement.Bottom;

        if (_ControlsPlacement == controlsPlacement)
        {
            return;
        }

        _ControlsPlacement = controlsPlacement;
        OnPropertyChanged(nameof(ControlsPlacement));
        OnControlsVisibilityChanged();
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        if (_ScreenRuntime is not null)
        {
            _ScreenRuntime.StreamFpsChanged -= OnStreamFpsChanged;
        }

        _SessionController.UiStateChanged -= OnUiStateChanged;
        _ScreenRuntime = null;
        ControlsViewModel.Dispose();
        _SessionController.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnStreamFpsChanged(double? streamFps)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateStreamFps(streamFps));
            return;
        }

        UpdateStreamFps(streamFps);
    }

    private void UpdateStreamFps(double? streamFps)
    {
        StreamFps = streamFps;
        OnPropertyChanged(nameof(StreamFps));
        StreamFpsChanged?.Invoke(streamFps);
    }

    private void OnUiStateChanged(SessionUiState state)
    {
        _ScreenRuntime?.UpdateSessionUiState(state);
    }

    private bool IsPortraitControlsRotation
    {
        get
        {
            float normalizedRotation = ((ControlsRotationDegrees % 360f) + 360f) % 360f;
            return Math.Abs(normalizedRotation - 90f) < 0.1f ||
                   Math.Abs(normalizedRotation - 270f) < 0.1f;
        }
    }

    private void OnControlsVisibilityChanged()
    {
        OnPropertyChanged(nameof(IsBottomControlsVisible));
        OnPropertyChanged(nameof(IsSideControlsVisible));
        OnPropertyChanged(nameof(IsBottomLandscapeControlsVisible));
        OnPropertyChanged(nameof(IsBottomPortraitControlsVisible));
        OnPropertyChanged(nameof(IsSideLandscapeControlsVisible));
        OnPropertyChanged(nameof(IsSidePortraitControlsVisible));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    internal ViewerSessionController SessionController => _SessionController;
}
