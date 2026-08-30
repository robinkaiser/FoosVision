// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.ComponentModel;
using System.Runtime.CompilerServices;
using FoosVision.Recorder.App.Runtime;

namespace FoosVision.Recorder.App;

public class RecorderConfigEditorViewModel : INotifyPropertyChanged
{
    private readonly RecorderConfigEditor _ConfigEditor;

    private string _ConfigText = string.Empty;
    private string _StatusText = string.Empty;
    private string _StatusDetail = string.Empty;
    private int _CursorPosition;
    private bool _IsVisible;

    public RecorderConfigEditorViewModel(RecorderConfigEditor configEditor)
    {
        _ConfigEditor = configEditor;
        OpenCommand = new Command(Open);
        SaveCommand = new Command(Save);
        CloseCommand = new Command(Close);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Command OpenCommand { get; }

    public Command SaveCommand { get; }

    public Command CloseCommand { get; }

    public string ConfigText
    {
        get => _ConfigText;
        set => SetProperty(ref _ConfigText, value ?? string.Empty);
    }

    public string StatusText
    {
        get => _StatusText;
        private set
        {
            if (SetProperty(ref _StatusText, value))
            {
                OnPropertyChanged(nameof(IsStatusVisible));
            }
        }
    }

    public string StatusDetail
    {
        get => _StatusDetail;
        private set
        {
            if (SetProperty(ref _StatusDetail, value))
            {
                OnPropertyChanged(nameof(IsStatusDetailVisible));
            }
        }
    }

    public bool IsStatusVisible => !string.IsNullOrWhiteSpace(StatusText);

    public bool IsStatusDetailVisible => !string.IsNullOrWhiteSpace(StatusDetail);

    public int CursorPosition
    {
        get => _CursorPosition;
        set => SetProperty(ref _CursorPosition, value);
    }

    public bool IsVisible
    {
        get => _IsVisible;
        private set => SetProperty(ref _IsVisible, value);
    }

    private void Open()
    {
        try
        {
            ConfigText = _ConfigEditor.LoadText();
            ResetTextPosition();
            StatusText = string.Empty;
            StatusDetail = string.Empty;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            ConfigText = string.Empty;
            ResetTextPosition();
            StatusText = "Config unavailable.";
            StatusDetail = ex.Message;
        }

        IsVisible = true;
    }

    private void Save()
    {
        RecorderConfigSaveResult result = _ConfigEditor.SaveText(ConfigText);

        switch (result.Status)
        {
            case RecorderConfigSaveStatus.Saved:
                StatusText = "Successfully changed. Restart required.";
                StatusDetail = string.Empty;
                break;
            case RecorderConfigSaveStatus.InvalidConfig:
                StatusText = "Invalid config. Changes not saved.";
                StatusDetail = result.Error ?? string.Empty;
                break;
            case RecorderConfigSaveStatus.SaveFailed:
                StatusText = "Save failed. Changes not saved.";
                StatusDetail = result.Error ?? string.Empty;
                break;
        }
    }

    private void Close()
    {
        IsVisible = false;
    }

    private void ResetTextPosition()
    {
        _CursorPosition = 0;
        OnPropertyChanged(nameof(CursorPosition));
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
