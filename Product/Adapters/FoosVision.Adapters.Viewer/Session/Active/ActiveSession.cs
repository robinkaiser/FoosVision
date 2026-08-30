// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Common.Logging;
using FoosVision.Common.Metrics;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.Adapters.Viewer.Session.Active;

public class ActiveSession :
    IRecorderRuntimeStateSink,
    IDisposable
{
    private static readonly Source _Log = new("Viewer.Session.ActiveSession");
    private static readonly TimeSpan _TrackingFpsPublishInterval = TimeSpan.FromMilliseconds(500);

    private readonly IConnectedViewerSession _Session;
    private readonly IUiStateSink _UiStateSink;
    private readonly IOverlaySink _OverlaySink;
    private readonly Timer _TrackingFpsTimer;
    private readonly ViewerPlaybackCoordinator _PlaybackCoordinator;
    private readonly IReplaySessionStore _ReplaySessionStore;
    private readonly IEncodedVisionContextConsumer _VisionContextConsumer;
    private readonly ViewerReplayCoordinator _ReplayCoordinator;
    private readonly BallDetectionMaskOverlayPresenter _BallDetectionMaskOverlayPresenter;
    private readonly LiveTrackingPresenter _LiveTrackingPresenter;
    private readonly TableUpdatePresenter _TableUpdatePresenter;
    private readonly IntervalMetric? _TrackingFrameHandleInterval;
    private readonly IDisposable _TableUpdateSubscription;
    private readonly IDisposable _TrackingFrameSubscription;
    private readonly IDisposable _VisionContextSubscription;
    private readonly IDisposable _BallDetectionMaskSubscription;
    private readonly IDisposable _ReplayStartedSubscription;
    private readonly IDisposable _ReplaySubscription;
    private RecorderRuntimeStateChanged _LastKnownRuntimeState = new()
    {
        Sequence = 0,
        Mode = RecorderRuntimeMode.Idle,
        ActiveSessionId = null,
        Reason = RecorderStateChangeReason.None,
        Detail = string.Empty,
    };
    private SessionUiState _UiState = new(SessionMode.Install, false, true, false, false);
    private ActiveSessionPendingIntent _PendingIntent;
    private bool _HasVisionContext;
    private int _Disposed;

    public ActiveSession(
        IConnectedViewerSession session,
        IUiStateSink uiStateSink,
        IOverlaySink overlaySink,
        IPlaybackSourceFactory playbackSourceFactory,
        IPlaybackController playbackController,
        IReplaySessionStore replaySessionStore,
        IEncodedVisionContextConsumer visionContextConsumer,
        IBallFinder ballFinder,
        IEncodedBallDetectionMaskDecoder ballDetectionMaskDecoder,
        IEncodedReplayFrameDecoder replayFrameDecoder,
        Func<DateTimeOffset>? utcNow = null,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _Session = session;
        _UiStateSink = uiStateSink;
        _OverlaySink = overlaySink;
        _PlaybackCoordinator = new ViewerPlaybackCoordinator(playbackSourceFactory, playbackController);
        _VisionContextConsumer = visionContextConsumer;
        _ReplaySessionStore = replaySessionStore;
        _ReplayCoordinator = new ViewerReplayCoordinator(
            _OverlaySink,
            _PlaybackCoordinator,
            _ReplaySessionStore,
            ballFinder,
            replayFrameDecoder,
            () => LatestTableConfiguration,
            () => _HasVisionContext,
            ResetTrackingOverlay,
            StartLivePlaybackAsync,
            UpdateTrackingFps);
        Func<DateTimeOffset> utcNowProvider = utcNow ?? (() => DateTimeOffset.UtcNow);
        TrackingOverlayProjector trackingProjector = new();
        _LiveTrackingPresenter = new(
            _OverlaySink,
            trackingProjector,
            utcNowProvider,
            () => _ReplayCoordinator.IsReplayPending,
            () => _ReplaySessionStore.HasActive,
            _ReplayCoordinator.ObserveLiveTrackingAsync);
        _TableUpdatePresenter = new(
            _OverlaySink,
            _LiveTrackingPresenter.UpdateTableConfiguration,
            _LiveTrackingPresenter.ResetProjection,
            RefreshUiState);
        _BallDetectionMaskOverlayPresenter = new(
            _OverlaySink,
            ballDetectionMaskDecoder,
            IsReplayActiveForUi);
        _TrackingFpsTimer = new Timer(_ => RefreshTrackingFps(), null, _TrackingFpsPublishInterval, _TrackingFpsPublishInterval);

        RuntimeMetricsOptions options = runtimeMetricsOptions ?? RuntimeMetricsOptions.CreateDefault();

        if (options.Enabled)
        {
            _TrackingFrameHandleInterval = new IntervalMetric(
                options.CreateMetricName("Viewer.TrackingFrame.HandleInterval"),
                _Log,
                options.GetReportInterval());
        }

        _Session.AttachRuntimeStateSink(this);
        _TableUpdateSubscription = _Session.LiveDataSubscriber.Subscribe<TableUpdateMessage>(_TableUpdatePresenter.Handle);
        _TrackingFrameSubscription = _Session.LiveDataSubscriber.Subscribe<TrackingFrameMessage>(OnTrackingFrameReceived);
        _VisionContextSubscription = _Session.LiveAnalysisSubscriber.Subscribe<VisionContextMessage>(OnVisionContextReceived);
        _BallDetectionMaskSubscription = _Session.LiveAnalysisSubscriber.Subscribe<BallDetectionMaskMessage>(_BallDetectionMaskOverlayPresenter.Handle);
        _ReplayStartedSubscription = _Session.LiveAnalysisSubscriber.Subscribe<ReplayStartedMessage>(_ReplayCoordinator.HandleReplayStarted);
        _ReplaySubscription = _Session.LiveAnalysisSubscriber.Subscribe<ReplayMessage>(_ReplayCoordinator.HandleReplay);

        PublishUiState();
    }

    public async Task ToggleModeSessionAsync(SessionMode mode)
    {
        if (!SessionUiStateCalculator.CanToggle(
            _LastKnownRuntimeState.Mode,
            _PendingIntent,
            mode,
            LatestTableConfiguration.HasValue))
        {
            return;
        }

        if (mode == SessionMode.Install)
        {
            var commandId = Guid.NewGuid();
            _PendingIntent = SessionUiStateCalculator.GetPendingIntent(_LastKnownRuntimeState.Mode, mode);
            RefreshUiState();

            try
            {
                CommandResponse response;
                if (_PendingIntent == ActiveSessionPendingIntent.StopInstall)
                {
                    _Log.Information("Sending StopInstall command {0}.", commandId);
                    response = await _Session.StopInstallAsync(commandId, CancellationToken.None);
                }
                else
                {
                    await StartLivePlaybackAsync();
                    _Log.Information("Sending StartInstall command {0}.", commandId);
                    response = await _Session.StartInstallAsync(commandId, CancellationToken.None);
                }

                if (!HandleCommandResponse(response, _PendingIntent))
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                _PendingIntent = ActiveSessionPendingIntent.None;
                _Log.Error("Install command send failed.", ex);
                RefreshUiState();
            }

            return;
        }

        var gameCommandId = Guid.NewGuid();
        _PendingIntent = SessionUiStateCalculator.GetPendingIntent(_LastKnownRuntimeState.Mode, mode);
        RefreshUiState();

        try
        {
            CommandResponse response;
            if (_PendingIntent == ActiveSessionPendingIntent.StopGame)
            {
                _Log.Information("Sending StopGame command {0}.", gameCommandId);
                response = await _Session.StopGameAsync(gameCommandId, CancellationToken.None);
            }
            else
            {
                await StartLivePlaybackAsync();
                _Log.Information("Sending StartGame command {0}.", gameCommandId);
                response = await _Session.StartGameAsync(gameCommandId, CancellationToken.None);
            }

            if (!HandleCommandResponse(response, _PendingIntent))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            _PendingIntent = ActiveSessionPendingIntent.None;
            _Log.Error("Game command send failed.", ex);
            RefreshUiState();
        }
    }

    // IRecorderRuntimeStateSink

    public void OnRecorderRuntimeStateChanged(RecorderRuntimeStateChanged state)
    {
        RecorderRuntimeMode previousMode = _LastKnownRuntimeState.Mode;
        _LastKnownRuntimeState = state;
        _PendingIntent = ActiveSessionPendingIntent.None;
        _Log.Information(
            "Recorder runtime state changed. Sequence={0} Mode={1} SessionId={2} Reason={3} Detail={4}",
            state.Sequence,
            state.Mode,
            state.ActiveSessionId,
            state.Reason,
            state.Detail);

        if (previousMode == RecorderRuntimeMode.GameRunning &&
            state.Mode != RecorderRuntimeMode.GameRunning)
        {
            ResetTrackingOverlay();
        }

        if (previousMode is RecorderRuntimeMode.GameRunning or RecorderRuntimeMode.InstallRunning &&
            state.Mode is not RecorderRuntimeMode.GameRunning and not RecorderRuntimeMode.InstallRunning)
        {
            _ReplayCoordinator.CancelReplayReplacement();
            _ = _ReplayCoordinator.StopReplayAsync();
            _ = _PlaybackCoordinator.StopAsync();
        }

        if (previousMode != state.Mode &&
            state.Mode is RecorderRuntimeMode.GameRunning or RecorderRuntimeMode.InstallRunning)
        {
            ResetTrackingOverlay();
        }

        RefreshUiState();
    }

    // IDisposable

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        if (Interlocked.Exchange(ref _Disposed, 1) != 0)
        {
            return;
        }

        _TrackingFrameSubscription.Dispose();
        _TableUpdateSubscription.Dispose();
        _VisionContextSubscription.Dispose();
        _BallDetectionMaskSubscription.Dispose();
        _ReplayStartedSubscription.Dispose();
        _ReplaySubscription.Dispose();
        _TrackingFpsTimer.Dispose();

        _ReplayCoordinator.Dispose();
        _PlaybackCoordinator.TryStopIgnoringErrors();
        _PlaybackCoordinator.Dispose();
    }

    internal Option<TableConfiguration> LatestTableConfiguration => _TableUpdatePresenter.LatestTableConfiguration;

    private void PublishUiState()
    {
        _UiStateSink.Update(_UiState);
    }

    private void OnVisionContextReceived(VisionContextMessage message)
    {
        EncodedVisionContext visionContext = new(message.Buffer, message.Length);

        if (!_VisionContextConsumer.TryApplyEncodedVisionContext(visionContext))
        {
            _Log.Warning("Vision context update ignored because the payload is invalid.");
            return;
        }

        _HasVisionContext = true;
    }

    private void OnTrackingFrameReceived(TrackingFrameMessage message)
    {
        _TrackingFrameHandleInterval?.Record();
        _LiveTrackingPresenter.Handle(message);
    }

    private bool HandleCommandResponse(CommandResponse response, ActiveSessionPendingIntent intent)
    {
        if (response.Accepted)
        {
            _Log.Information(
                "Recorder accepted {0}. CommandId={1}",
                intent,
                response.CommandId);
            return true;
        }

        _PendingIntent = ActiveSessionPendingIntent.None;
        _Log.Warning(
            "Recorder rejected {0}. CommandId={1} Error={2}",
            intent,
            response.CommandId,
            response.Error);
        RefreshUiState();
        return false;
    }

    private void RefreshUiState()
    {
        bool isReplayActive = IsReplayActiveForUi();
        _UiState = SessionUiStateCalculator.Calculate(
            _UiState,
            _LastKnownRuntimeState.Mode,
            _PendingIntent,
            LatestTableConfiguration.HasValue,
            isReplayActive);
        PublishUiState();
    }

    private void ResetTrackingOverlay()
    {
        _LiveTrackingPresenter.Reset();
        _ReplayCoordinator.ResetAnalysis();

        UpdateTrackingFps(IsReplayActiveForUi() ? SessionUiStateCalculator.ReplayTrackingFps : null);
        _OverlaySink.ClearTrackingState();
        _OverlaySink.ClearBallDetectionMaskState();
    }

    private void RefreshTrackingFps()
    {
        if (Interlocked.CompareExchange(ref _Disposed, 0, 0) != 0)
        {
            return;
        }

        UpdateTrackingFps(_LiveTrackingPresenter.GetFramesPerSecond());
    }

    private void UpdateTrackingFps(double? trackingFps)
    {
        bool isReplayActive = IsReplayActiveForUi();
        SessionUiState nextState = SessionUiStateCalculator.UpdateTrackingFps(
            _UiState,
            trackingFps,
            isReplayActive);

        if (_UiState.TrackingFps == nextState.TrackingFps &&
            _UiState.IsReplayActive == nextState.IsReplayActive)
        {
            return;
        }

        _UiState = nextState;
        PublishUiState();
    }

    private async Task StartLivePlaybackAsync()
    {
        ResetTrackingOverlay();
        await _PlaybackCoordinator.StartLiveAsync(_Session.Connection);
    }

    private bool IsReplayActiveForUi()
        => _ReplayCoordinator.IsReplayActive;
}
