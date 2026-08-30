// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Media.Core.Capture;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Recorder.Composition;

namespace FoosVision.Recorder.App.Runtime;

public class RecorderRuntime : IRecorderRuntime
{
    private static readonly Source _Log = new("Recorder.Runtime");

    private readonly ICameraFeed _CameraFeed;
    private readonly MainViewModel _ViewModel;
    private readonly RecorderHost _RecorderHost;
    private readonly IDisposable? _ProcessNetworkBinding;
    private bool _Started;

    public RecorderRuntime(
        ICameraFeed cameraFeed,
        MainViewModel viewModel,
        IVideoDumpWriter videoDumpWriter,
        bool publishObservations = false,
        bool publishBallDetectionMask = false,
        RuntimeMetricsOptions? runtimeMetricsOptions = null,
        IDisposable? processNetworkBinding = null)
    {
        _CameraFeed = cameraFeed;
        _ViewModel = viewModel;
        _ProcessNetworkBinding = processNetworkBinding;

        RecorderVersionProvider versionProvider = new();

        _RecorderHost = new RecorderHost(
            _CameraFeed,
            versionProvider,
            new RecorderHandshakeDiagnosticsProvider(),
            new RecorderHandshakeViewerSettingsProvider(),
            videoDumpWriter,
            publishObservations,
            publishBallDetectionMask,
            runtimeMetricsOptions);

        _RecorderHost.ViewerConnected += OnViewerConnected;
        _RecorderHost.RuntimeStateChanged += OnRecorderRuntimeStateChanged;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_Started)
        {
            return;
        }

        PermissionStatus cameraPermission = await Permissions.RequestAsync<Permissions.Camera>().WaitAsync(ct);

        if (cameraPermission != PermissionStatus.Granted)
        {
            _ViewModel.ShowFault("Camera permission missing");
            _Log.Error("Camera permission was not granted.");
            return;
        }

        _Log.Information("Starting recorder host.");
        _RecorderHost.Start();
        _Started = true;
        _ViewModel.ShowReady();
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (!_Started)
        {
            return;
        }

        await _RecorderHost.StopActiveSessions(ct);
        _Started = false;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _Log.Information("Disposing recorder runtime.");
        _RecorderHost.ViewerConnected -= OnViewerConnected;
        _RecorderHost.RuntimeStateChanged -= OnRecorderRuntimeStateChanged;
        _RecorderHost.Dispose();
        _ProcessNetworkBinding?.Dispose();
    }

    private void OnViewerConnected()
    {
        _ViewModel.ShowConnected();
    }

    private void OnRecorderRuntimeStateChanged(RecorderRuntimeStateChanged state)
    {
        _ViewModel.ShowRuntimeMode(state.Mode);
    }
}
