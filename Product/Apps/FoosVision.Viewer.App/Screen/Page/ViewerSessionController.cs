// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Windows.Input;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Viewer.Composition;
using FoosVision.Viewer.App.Runtime;
using FoosVision.Viewer.App.Screen.Stage;
using FoosVision.Viewer.App.Platforms.Android.Connectivity;

namespace FoosVision.Viewer.App.Screen.Page;

public class ViewerSessionController :
    IUiStateSink,
    IDisposable
{
    private readonly CancellationTokenSource _ConnectCts = new();
    private readonly IViewerSessionHost _ViewerHost;
    private bool _Disposed;
    private bool _RuntimeAttached;
    private SessionManager? _SessionManager;

    public ViewerSessionController()
    {
        _ViewerHost = new ViewerHost(
            fallbackCandidateSource: new AndroidRecorderFallbackCandidateSource(Platform.AppContext));
        ToggleInstallModeCommand = new Command(async () => await ToggleModeSessionAsync(SessionMode.Install));
        ToggleGameModeCommand = new Command(async () => await ToggleModeSessionAsync(SessionMode.Game));
    }

    public event Action<SessionUiState>? UiStateChanged;

    public SessionUiState CurrentUiState { get; private set; } = new(SessionMode.Install, false, false, true, false);

    public ICommand ToggleInstallModeCommand { get; }

    public ICommand ToggleGameModeCommand { get; }

    public async Task AttachRuntimeAsync(IViewerScreenRuntime runtime)
    {
        if (_RuntimeAttached)
        {
            return;
        }

        _RuntimeAttached = true;
        _SessionManager = new SessionManager(
            this,
            runtime.OverlaySink,
            runtime.PlaybackSourceFactory,
            runtime.PlaybackController,
            _ViewerHost,
            ViewerLoggingBootstrap.ApplyHandshakeDiagnostics);

        await _SessionManager.InitializeAsync(_ConnectCts.Token);
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        _ConnectCts.Cancel();
        _ConnectCts.Dispose();
        _SessionManager?.Dispose();
        GC.SuppressFinalize(this);
    }

    public Task ToggleModeSessionAsync(SessionMode mode)
    {
        return _SessionManager?.ToggleModeSessionAsync(mode) ?? Task.CompletedTask;
    }

    void IUiStateSink.Update(SessionUiState uiState)
    {
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => UpdateUiState(uiState));
            return;
        }

        UpdateUiState(uiState);
    }

    private void UpdateUiState(SessionUiState uiState)
    {
        CurrentUiState = uiState;
        UiStateChanged?.Invoke(uiState);
    }
}
