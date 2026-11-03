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

    private readonly IConnectedViewerSession _Session;
    private readonly IUiStateSink _UiStateSink;
    private readonly IOverlaySink _OverlaySink;
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
    private readonly IDisposable _ProcessFrameRateSubscription;
    private readonly IDisposable _VisionContextSubscription;
    private readonly IDisposable _BallDetectionMaskSubscription;
    private readonly IDisposable _ReplayStartedSubscription;
    private readonly IDisposable _ReplayClipSubscription;
    private readonly IDisposable _ReplayReturnToLiveSubscription;
    private RecorderRuntimeStateChanged _LastKnownRuntimeState = new()
    {
        Sequence = 0,
        Mode = RecorderRuntimeMode.Idle,
        ActiveSessionId = null,
        Reason = RecorderStateChangeReason.None,
        Detail = string.Empty,
    };
    private SessionUiState _UiState = new(SessionMode.Setup, false, true, false, false);
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
            RefreshUiState);
        TrackingOverlayProjector trackingProjector = new();
        _LiveTrackingPresenter = new(
            _OverlaySink,
            trackingProjector,
            () => _ReplayCoordinator.IsReplayPending,
            () => _ReplaySessionStore.HasActive);
        _TableUpdatePresenter = new(
            _OverlaySink,
            _LiveTrackingPresenter.UpdateTableConfiguration,
            _LiveTrackingPresenter.ResetProjection,
            RefreshUiState);
        _BallDetectionMaskOverlayPresenter = new(
            _OverlaySink,
            ballDetectionMaskDecoder,
            IsReplayActiveForUi);
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
        _ProcessFrameRateSubscription = _Session.LiveDataSubscriber.Subscribe<ProcessFrameRateMessage>(OnProcessFrameRateReceived);
        _VisionContextSubscription = _Session.LiveAnalysisSubscriber.Subscribe<VisionContextMessage>(OnVisionContextReceived);
        _BallDetectionMaskSubscription = _Session.LiveAnalysisSubscriber.Subscribe<BallDetectionMaskMessage>(_BallDetectionMaskOverlayPresenter.Handle);
        _ReplayReturnToLiveSubscription = _Session.LiveAnalysisSubscriber.Subscribe<ReplayReturnToLiveMessage>(_ReplayCoordinator.HandleReplayReturnToLive);
        _ReplayStartedSubscription = _Session.LiveAnalysisSubscriber.Subscribe<ReplayStartedMessage>(_ReplayCoordinator.HandleReplayStarted);
        _ReplayClipSubscription = _Session.LiveAnalysisSubscriber.Subscribe<ReplayClipMessage>(_ReplayCoordinator.HandleReplayClip);

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

        switch (mode)
        {
            case SessionMode.Setup:
                await ToggleSessionAsync(
                    mode,
                    ActiveSessionPendingIntent.StopSetup,
                    _Session.StartSetupAsync,
                    _Session.StopSetupAsync);
                break;
            case SessionMode.Game:
                await ToggleSessionAsync(
                    mode,
                    ActiveSessionPendingIntent.StopGame,
                    _Session.StartGameAsync,
                    _Session.StopGameAsync);
                break;
            case SessionMode.Training:
                await ToggleSessionAsync(
                    mode,
                    ActiveSessionPendingIntent.StopTraining,
                    _Session.StartTrainingAsync,
                    _Session.StopTrainingAsync);
                break;
        }
    }

    // IRecorderRuntimeStateSink

    public void OnRecorderRuntimeStateChanged(RecorderRuntimeStateChanged state)
    {
        RecorderRuntimeMode previousMode = _LastKnownRuntimeState.Mode;
        bool sessionChanged = _LastKnownRuntimeState.ActiveSessionId != state.ActiveSessionId;
        bool supportsReplay = state.Mode is RecorderRuntimeMode.GameRunning or RecorderRuntimeMode.TrainingRunning;
        _ReplayCoordinator.SetRecorderSession(supportsReplay ? state.ActiveSessionId : null);
        _LastKnownRuntimeState = state;
        _PendingIntent = ActiveSessionPendingIntent.None;
        _Log.Information(
            "Recorder runtime state changed. Sequence={0} Mode={1} SessionId={2} Reason={3} Detail={4}",
            state.Sequence,
            state.Mode,
            state.ActiveSessionId,
            state.Reason,
            state.Detail);

        if (previousMode is RecorderRuntimeMode.GameRunning or RecorderRuntimeMode.TrainingRunning &&
            previousMode != state.Mode)
        {
            ResetTrackingOverlay();
        }

        if (previousMode is RecorderRuntimeMode.GameRunning or RecorderRuntimeMode.SetupRunning or RecorderRuntimeMode.TrainingRunning &&
            state.Mode is not RecorderRuntimeMode.GameRunning and not RecorderRuntimeMode.SetupRunning and not RecorderRuntimeMode.TrainingRunning)
        {
            _ReplayCoordinator.CancelReplayReplacement();
            _ = _ReplayCoordinator.StopReplayAsync();
            _ = _PlaybackCoordinator.StopAsync();
        }

        if ((previousMode != state.Mode || sessionChanged) &&
            state.Mode is RecorderRuntimeMode.GameRunning or RecorderRuntimeMode.SetupRunning or RecorderRuntimeMode.TrainingRunning)
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
        _ProcessFrameRateSubscription.Dispose();
        _TableUpdateSubscription.Dispose();
        _VisionContextSubscription.Dispose();
        _BallDetectionMaskSubscription.Dispose();
        _ReplayStartedSubscription.Dispose();
        _ReplayClipSubscription.Dispose();
        _ReplayReturnToLiveSubscription.Dispose();
        _ReplayCoordinator.Dispose();
        _ReplaySessionStore.Clear();
        ResetTrackingOverlay();
        _OverlaySink.UpdateTableState(TableOverlayState.Empty);
        _PlaybackCoordinator.TryStopIgnoringErrors();
        _PlaybackCoordinator.Dispose();
        _UiState = new(SessionMode.Setup, false, false, false, false);
        PublishUiState();
    }

    internal Option<TableConfiguration> LatestTableConfiguration => _TableUpdatePresenter.LatestTableConfiguration;

    private async Task ToggleSessionAsync(
        SessionMode mode,
        ActiveSessionPendingIntent stopIntent,
        Func<Guid, CancellationToken, Task<CommandResponse>> start,
        Func<Guid, CancellationToken, Task<CommandResponse>> stop)
    {
        Guid commandId = Guid.NewGuid();
        _PendingIntent = SessionUiStateCalculator.GetPendingIntent(_LastKnownRuntimeState.Mode, mode);
        RefreshUiState();

        try
        {
            CommandResponse response;
            if (_PendingIntent == stopIntent)
            {
                _Log.Information("Sending Stop{0} command {1}.", mode, commandId);
                response = await stop(commandId, CancellationToken.None);
            }
            else
            {
                await StartLivePlaybackAsync();
                _Log.Information("Sending Start{0} command {1}.", mode, commandId);
                response = await start(commandId, CancellationToken.None);
            }

            if (!HandleCommandResponse(response, _PendingIntent))
            {
                return;
            }
        }
        catch (Exception ex)
        {
            _PendingIntent = ActiveSessionPendingIntent.None;
            _Log.Error("{0} command send failed.", mode, ex);
            RefreshUiState();
        }
    }

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

    private void OnProcessFrameRateReceived(ProcessFrameRateMessage message)
    {
        UpdateProcessFps(message.FramesPerSecond);
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

        _OverlaySink.ClearTrackingState();
        _OverlaySink.ClearBallDetectionMaskState();
    }

    private void UpdateProcessFps(double? processFps)
    {
        bool isReplayActive = IsReplayActiveForUi();
        SessionUiState nextState = SessionUiStateCalculator.UpdateProcessFps(
            _UiState,
            processFps,
            isReplayActive);

        if (_UiState.ProcessFps == nextState.ProcessFps &&
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
