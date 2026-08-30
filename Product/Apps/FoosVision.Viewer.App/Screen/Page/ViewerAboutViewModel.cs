// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FoosVision.Viewer.App.Screen.Page;

public class ViewerAboutViewModel : INotifyPropertyChanged
{
    private const string _NoticeFileName = "FoosVisionThirdPartyNotices.txt";

    private string _AboutText = string.Empty;
    private int _CursorPosition;
    private bool _IsVisible;

    public ViewerAboutViewModel()
    {
        OpenCommand = new Command(async () => await OpenAsync());
        CloseCommand = new Command(Close);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Command OpenCommand { get; }

    public Command CloseCommand { get; }

    public string AboutText
    {
        get => _AboutText;
        private set => SetProperty(ref _AboutText, value);
    }

    public bool IsVisible
    {
        get => _IsVisible;
        private set => SetProperty(ref _IsVisible, value);
    }

    public int CursorPosition
    {
        get => _CursorPosition;
        set => SetProperty(ref _CursorPosition, value);
    }

    private async Task OpenAsync()
    {
        IsVisible = true;
        AboutText = "Loading...";
        ResetTextPosition();

        try
        {
            using Stream stream = await FileSystem.OpenAppPackageFileAsync(_NoticeFileName);
            using StreamReader reader = new(stream);
            AboutText = await reader.ReadToEndAsync();
            ResetTextPosition();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AboutText = $"About information unavailable.{Environment.NewLine}{Environment.NewLine}{ex.Message}";
            ResetTextPosition();
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
