// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Common.Metrics;
using FoosVision.Media.Core.Capture;
using FoosVision.Protocol.Messages.Events;
using NetMQ;

namespace FoosVision.Recorder.Composition;

public class RecorderHost : IDisposable
{
    private readonly RecorderCompositionRoot _Root;
    private bool _Started;

    public RecorderHost(
        ICameraFeed cameraFeed,
        IRecorderVersionProvider versionProvider,
        IHandshakeDiagnosticsProvider diagnosticsProvider,
        IHandshakeViewerSettingsProvider viewerSettingsProvider,
        IVideoDumpWriter videoDumpWriter,
        bool publishObservations = false,
        bool publishBallDetectionMask = false,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _Root = RecorderCompositionRoot.Compose(
            cameraFeed,
            versionProvider,
            diagnosticsProvider,
            viewerSettingsProvider,
            videoDumpWriter,
            publishObservations,
            publishBallDetectionMask,
            runtimeMetricsOptions);
    }

    public event Action? ViewerConnected
    {
        add => _Root.ViewerConnected += value;
        remove => _Root.ViewerConnected -= value;
    }

    public event Action<RecorderRuntimeStateChanged>? RuntimeStateChanged
    {
        add => _Root.RuntimeStateChanged += value;
        remove => _Root.RuntimeStateChanged -= value;
    }

    public void Start()
    {
        if (_Started) return;

        _Root.Network.Start();
        _Started = true;
    }

    public Task StopActiveSessions(CancellationToken ct)
    {
        return _Root.StopActiveSessions(ct);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            _Root.Dispose();
        }
        catch
        {
        }

        // Note: process-wide cleanup
        NetMQConfig.Cleanup();
    }
}
