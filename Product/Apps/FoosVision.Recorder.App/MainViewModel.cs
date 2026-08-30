// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.ComponentModel;
using System.Runtime.CompilerServices;
using FoosVision.Protocol.Messages.Events;

namespace FoosVision.Recorder.App;

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly Color _ConnectedIndicatorColor = Color.FromArgb("#31D07F");
    private static readonly Color _DisconnectedIndicatorColor = Color.FromArgb("#5F6873");

    private bool _IsFaulted;
    private bool _IsModeVisible;
    private bool _IsViewerConnected;
    private string _ConnectionStatusText = "Waiting for Viewer";
    private Color _ConnectionStatusColor = _DisconnectedIndicatorColor;
    private string _ModeText = "Starting";

    public MainViewModel(
        RecorderConfigEditorViewModel configEditor,
        RecorderAboutViewModel about)
    {
        ConfigEditor = configEditor;
        About = about;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RecorderConfigEditorViewModel ConfigEditor { get; }

    public RecorderAboutViewModel About { get; }

    public string TitleText { get; } = CreateTitleText();

    public string ConnectionStatusText
    {
        get => _ConnectionStatusText;
        private set => SetProperty(ref _ConnectionStatusText, value);
    }

    public Color ConnectionStatusColor
    {
        get => _ConnectionStatusColor;
        private set => SetProperty(ref _ConnectionStatusColor, value);
    }

    public bool IsModeVisible
    {
        get => _IsModeVisible;
        private set => SetProperty(ref _IsModeVisible, value);
    }

    public string ModeText
    {
        get => _ModeText;
        private set => SetProperty(ref _ModeText, value);
    }

    public void ShowReady()
    {
        MainThread.BeginInvokeOnMainThread(() => SetMode("Ready"));
    }

    public void ShowConnected()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _IsViewerConnected = true;
            ConnectionStatusText = "Viewer connected";
            ConnectionStatusColor = _ConnectedIndicatorColor;
            IsModeVisible = true;
        });
    }

    public void ShowFault(string detail)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _IsFaulted = true;
            ModeText = $"Recorder fault: {detail}";
            IsModeVisible = true;
        });
    }

    public void ShowRuntimeMode(RecorderRuntimeMode mode)
    {
        string text = mode switch
        {
            RecorderRuntimeMode.Idle => "Ready",
            RecorderRuntimeMode.InstallRunning => "Installation",
            RecorderRuntimeMode.GameRunning => "Tracking",
            RecorderRuntimeMode.Faulted => "Recorder fault",
            _ => "Unknown mode",
        };

        MainThread.BeginInvokeOnMainThread(() => SetMode(text, mode == RecorderRuntimeMode.Faulted));
    }

    private void SetMode(string text, bool isFaulted = false)
    {
        _IsFaulted = isFaulted;
        ModeText = text;
        IsModeVisible = _IsFaulted || _IsViewerConnected;
    }

    private static string CreateTitleText()
    {
        string version = AppInfo.Current.VersionString;
        return string.IsNullOrWhiteSpace(version)
            ? "FoosVision Recorder"
            : $"FoosVision Recorder {version}";
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
