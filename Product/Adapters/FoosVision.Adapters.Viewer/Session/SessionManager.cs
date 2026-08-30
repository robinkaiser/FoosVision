// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.Adapters.Viewer.Session;

public class SessionManager : IDisposable
{
    private static readonly Source _Log = new("Viewer.Session.SessionManager");

    private readonly IUiStateSink _UiStateSink;
    private readonly Func<IConnectedViewerSession, ActiveSession> _CreateActiveSession;
    private readonly IViewerSessionHost _ViewerHost;
    private readonly IReplaySessionStore _ReplaySessionStore;
    private readonly IEncodedVisionContextConsumer _VisionContextConsumer;
    private readonly IBallFinder _BallFinder;
    private readonly IEncodedBallDetectionMaskDecoder _BallDetectionMaskDecoder;
    private readonly IEncodedReplayFrameDecoder _ReplayFrameDecoder;
    private readonly Action<RecorderConnection> _OnConnected;
    private ActiveSession? _ActiveSession;
    private SessionUiState _UiState = new(SessionMode.Install, false, false, true, false);
    private int _Disposed;

    public SessionManager(
        IUiStateSink uiStateSink,
        IOverlaySink overlaySink,
        IPlaybackSourceFactory playbackSourceFactory,
        IPlaybackController playbackController,
        IViewerSessionHost viewerHost,
        Action<RecorderConnection> onConnected)
    {
        _UiStateSink = uiStateSink;
        _ReplaySessionStore = viewerHost.ReplaySessionStore;
        _VisionContextConsumer = viewerHost.VisionContextConsumer;
        _BallFinder = viewerHost.BallFinder;
        _BallDetectionMaskDecoder = viewerHost.BallDetectionMaskDecoder;
        _ReplayFrameDecoder = viewerHost.ReplayFrameDecoder;
        _OnConnected = onConnected;

        _CreateActiveSession = session => new ActiveSession(
            session,
            uiStateSink,
            overlaySink,
            playbackSourceFactory,
            playbackController,
            _ReplaySessionStore,
            _VisionContextConsumer,
            _BallFinder,
            _BallDetectionMaskDecoder,
            _ReplayFrameDecoder,
            runtimeMetricsOptions: CreateRuntimeMetricsOptions(session));
        _ViewerHost = viewerHost;

        PublishUiState();
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return;
        }

        _Log.Information("Connecting viewer to recorder.");

        RecorderConnectionResult result;
        try
        {
            result = await _ViewerHost.ConnectAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }

        if (!result.Success)
        {
            if (ct.IsCancellationRequested ||
                result.Failure.Value == RecorderConnectionFailure.Cancelled)
            {
                return;
            }

            _Log.Warning(
                "Viewer failed to connect to recorder. Failure={0}",
                result.Failure.Value);
            _UiState = _UiState with
            {
                IsConnected = false,
                IsPendingCommand = false,
                IsFaulted = false,
            };
            PublishUiState();
            return;
        }

        var session = _ViewerHost.ConnectedSession.Value;

        _Log.Information(
            "Viewer connected to recorder. RecorderIp={0} ProtocolVersion={1} RecorderAppVersion={2}",
            session.Connection.RecorderIpAddress,
            session.Connection.ProtocolVersion,
            session.Connection.RecorderAppVersion);

        _OnConnected.Invoke(session.Connection);

        _ActiveSession?.Dispose();
        _ActiveSession = _CreateActiveSession(session);
    }

    public async Task ToggleModeSessionAsync(SessionMode mode)
    {
        if (_ActiveSession is null)
        {
            return;
        }

        await _ActiveSession.ToggleModeSessionAsync(mode);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Interlocked.Exchange(ref _Disposed, 1) != 0)
        {
            return;
        }

        _ActiveSession?.Dispose();
        _ViewerHost.Dispose();
    }

    private void PublishUiState()
    {
        _UiStateSink.Update(_UiState);
    }

    private static RuntimeMetricsOptions CreateRuntimeMetricsOptions(IConnectedViewerSession session)
    {
        var runtimeMetrics = session.Connection.Diagnostics.RuntimeMetrics;
        return new RuntimeMetricsOptions
        {
            Enabled = runtimeMetrics.Enabled,
            ReportInterval = TimeSpan.FromSeconds(Math.Max(1, runtimeMetrics.ReportIntervalSeconds)),
            NamePrefix = "Android",
        };
    }
}
