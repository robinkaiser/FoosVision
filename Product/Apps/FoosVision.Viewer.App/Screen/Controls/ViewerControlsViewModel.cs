// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Viewer.App.Screen.Page;

namespace FoosVision.Viewer.App.Screen.Controls;

public class ViewerControlsViewModel :
    INotifyPropertyChanged,
    IDisposable
{
    private readonly ViewerSessionController _SessionController;
    private readonly ViewerPageViewModel _ViewModel;
    private float _ControlsRotationDegrees;
    private string _TrackingFpsText = "Tracking ---";
    private string _StreamFpsText = "Stream ---";
    private bool _IsTrackingFpsVisible;
    private bool _IsStreamFpsVisible;
    private bool _IsReplayActive;

    public ViewerControlsViewModel(ViewerPageViewModel viewModel, ViewerSessionController sessionController)
    {
        _ViewModel = viewModel;
        _SessionController = sessionController;
        _ControlsRotationDegrees = viewModel.ControlsRotationDegrees;
        _SessionController.UiStateChanged += OnUiStateChanged;
        viewModel.ControlsRotationChanged += OnControlsRotationChanged;
        viewModel.StreamFpsChanged += OnStreamFpsChanged;
        InstallButton = CreateButtonState(SessionMode.Install);
        GameButton = CreateButtonState(SessionMode.Game);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ToggleInstallModeCommand => _SessionController.ToggleInstallModeCommand;

    public ICommand ToggleGameModeCommand => _SessionController.ToggleGameModeCommand;

    public ICommand OpenAboutCommand => _ViewModel.About.OpenCommand;

    public ViewerControlButtonState InstallButton { get; private set; }

    public ViewerControlButtonState GameButton { get; private set; }

    public string TrackingFpsText => _TrackingFpsText;

    public string StreamFpsText => _StreamFpsText;

    public string FpsLabelText => "FPS";

    public bool IsTrackingFpsVisible => _IsTrackingFpsVisible;

    public bool IsStreamFpsVisible => _IsStreamFpsVisible;

    public float ControlsRotationDegrees => _ControlsRotationDegrees;

    public void Dispose()
    {
        _SessionController.UiStateChanged -= OnUiStateChanged;
        _ViewModel.ControlsRotationChanged -= OnControlsRotationChanged;
        _ViewModel.StreamFpsChanged -= OnStreamFpsChanged;
        GC.SuppressFinalize(this);
    }

    private void OnUiStateChanged(SessionUiState uiState)
    {
        InstallButton = CreateButtonState(SessionMode.Install);
        GameButton = CreateButtonState(SessionMode.Game);

        _IsReplayActive = uiState.IsReplayActive;
        _IsTrackingFpsVisible = uiState.IsRunning && uiState.Mode == SessionMode.Game;
        _IsStreamFpsVisible = uiState.IsRunning;

        _TrackingFpsText = FormatTrackingFpsText(uiState);
        UpdateStreamFpsText(_ViewModel.StreamFps);

        OnPropertyChanged(nameof(InstallButton));
        OnPropertyChanged(nameof(GameButton));
        OnPropertyChanged(nameof(TrackingFpsText));
        OnPropertyChanged(nameof(StreamFpsText));
        OnPropertyChanged(nameof(IsTrackingFpsVisible));
        OnPropertyChanged(nameof(IsStreamFpsVisible));
    }

    private void OnStreamFpsChanged(double? streamFps)
    {
        UpdateStreamFpsText(streamFps);
        OnPropertyChanged(nameof(StreamFpsText));
    }

    private void UpdateStreamFpsText(double? streamFps)
    {
        _StreamFpsText = !_IsReplayActive && streamFps.HasValue
            ? $"Stream {streamFps.Value:0.0}"
            : "Stream ---";
    }

    private static string FormatTrackingFpsText(SessionUiState uiState)
    {
        return uiState.TrackingFps.HasValue ?
            $"Tracking {uiState.TrackingFps.Value:0.0}" :
            "Tracking ---";
    }

    private void OnControlsRotationChanged(float rotationDegrees)
    {
        if (Math.Abs(_ControlsRotationDegrees - rotationDegrees) < 0.1f)
        {
            return;
        }

        _ControlsRotationDegrees = rotationDegrees;
        OnPropertyChanged(nameof(ControlsRotationDegrees));
    }

    private ViewerControlButtonState CreateButtonState(SessionMode mode)
    {
        return new ViewerControlButtonState(
            GetButtonText(mode),
            GetButtonEnabled(mode),
            GetButtonBackground(mode),
            GetButtonTextColor(mode),
            GetButtonBorderColor(mode));
    }

    private string GetButtonText(SessionMode mode)
    {
        return IsRunningMode(mode)
            ? $"Stop {GetModeLabel(mode)}"
            : $"Start {GetModeLabel(mode)}";
    }

    private bool GetButtonEnabled(SessionMode mode)
    {
        SessionUiState uiState = _SessionController.CurrentUiState;
        bool isRunningMode = IsRunningMode(mode);

        if (!uiState.IsConnected ||
            uiState.IsFaulted ||
            uiState.IsPendingCommand)
        {
            return false;
        }

        if (uiState.IsRunning)
        {
            return isRunningMode;
        }

        return CanStartMode(mode, uiState);
    }

    private static bool CanStartMode(SessionMode mode, SessionUiState uiState)
    {
        return mode switch
        {
            SessionMode.Install => true,
            SessionMode.Game => uiState.IsGameAvailable,
            _ => false,
        };
    }

    private Color GetButtonTextColor(SessionMode mode)
    {
        if (IsRunningMode(mode))
        {
            return Color.FromArgb("#300B0B");
        }

        return GetButtonEnabled(mode)
            ? Color.FromArgb("#0A1B12")
            : Color.FromArgb("#9298A1");
    }

    private Color GetButtonBackground(SessionMode mode)
    {
        if (IsRunningMode(mode))
        {
            return Color.FromArgb("#FF7979");
        }

        return GetButtonEnabled(mode)
            ? Color.FromArgb("#81E5BA")
            : Color.FromArgb("#761C222C");
    }

    private Color GetButtonBorderColor(SessionMode mode)
    {
        if (IsRunningMode(mode))
        {
            return Color.FromArgb("#FFA4A4");
        }

        return GetButtonEnabled(mode)
            ? Color.FromArgb("#C6F4DE")
            : Color.FromArgb("#94637080");
    }

    private bool IsRunningMode(SessionMode mode)
    {
        SessionUiState uiState = _SessionController.CurrentUiState;
        return uiState.IsRunning && uiState.Mode == mode;
    }

    private static string GetModeLabel(SessionMode mode)
    {
        return mode == SessionMode.Install ? "Install" : "Game";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
