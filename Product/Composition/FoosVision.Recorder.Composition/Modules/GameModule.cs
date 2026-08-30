// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Adapters.Recorder.Game;
using FoosVision.Adapters.Recorder.Game.Control;
using FoosVision.Adapters.Recorder.Game.Live;
using FoosVision.Adapters.Recorder.Game.Orchestration;
using FoosVision.Common.Metrics;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Recorder.Composition.InMemoryStores;
using FoosVision.UseCases.Calibration.Ports;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Calibration.UpdateTableScene;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.CompleteTableSceneUpdate;
using FoosVision.UseCases.Game.CompleteTableUpdate;
using FoosVision.UseCases.Game.Ports;
using FoosVision.UseCases.Game.ProcessFrame;
using FoosVision.UseCases.Game.StartGame;
using FoosVision.UseCases.Game.StopGame;

namespace FoosVision.Recorder.Composition.Modules;

internal class GameModule : IDisposable
{
    private readonly IRecorderEventPublisher _EventPublisher;
    private readonly RecorderRuntimeStateController _RuntimeState;
    private readonly ReplayCoordinator _ReplayCoordinator;
    private readonly VisionContextUpdatePublisher _VisionContextUpdatePublisher;
    private readonly IVideoDumpOrchestrator _VideoDumpOrchestrator;

    public GameModule(
        IFrameSource frameSource,
        IFrameFeed frameFeed,
        IEncodedReplayBuffer replayBuffer,
        ILiveVideoStreamController liveVideoStreamController,
        IBallFinder ballFinder,
        IEncodedBallDetectionMaskProvider ballDetectionMaskProvider,
        ITableConfigFinder tableConfigFinder,
        ITableSceneUpdater tableSceneUpdater,
        IEncodedVisionContextProvider visionContextProvider,
        ISettingsStore settingsStore,
        ITableConfigStore tableConfigStore,
        IRecorderEventPublisher eventPublisher,
        RecorderRuntimeStateController runtimeState,
        IRecorderLiveDataPublisher liveDataPublisher,
        IRecorderLiveAnalysisPublisher liveAnalysisPublisher,
        IUpdateTableSceneOutputPort updateTableScenePresenter,
        IVideoDumpOrchestrator videoDumpOrchestrator,
        bool publishObservations = false,
        bool publishBallDetectionMask = false,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _EventPublisher = eventPublisher;
        _RuntimeState = runtimeState;
        _VideoDumpOrchestrator = videoDumpOrchestrator;
        SessionStore = new GameSessionStore();

        StartGame = new StartGameInteractor(SessionStore, settingsStore, frameSource);
        StopGame = new StopGameInteractor(SessionStore, frameSource);
        ProcessFrame = new ProcessFrameInteractor(SessionStore);
        CompleteTableUpdate = new CompleteTableUpdateInteractor(SessionStore);
        CompleteTableSceneUpdate = new CompleteTableSceneUpdateInteractor(SessionStore);

        var updateTable = new UpdateTableInteractor(tableConfigStore);
        var tableUpdatePresenter = new TableUpdatePresenter(SessionStore, liveDataPublisher);
        var updateTableScene = new UpdateTableSceneInteractor(tableConfigStore);

        var calibration = new CalibrationCoordinator(
            updateTable,
            tableUpdatePresenter,
            tableConfigFinder,
            updateTableScene,
            updateTableScenePresenter,
            tableSceneUpdater,
            frameFeed,
            CompleteTableUpdate,
            CompleteTableSceneUpdate);

        _ReplayCoordinator = new ReplayCoordinator(replayBuffer, liveVideoStreamController, liveAnalysisPublisher);

        _VisionContextUpdatePublisher = new VisionContextUpdatePublisher(
            visionContextProvider,
            liveAnalysisPublisher,
            () => SessionStore.HasActive);

        var framePresenter = new FramePresenter(
            liveDataPublisher,
            calibration,
            _ReplayCoordinator,
            liveAnalysisPublisher,
            ballDetectionMaskProvider,
            publishObservations,
            publishBallDetectionMask);

        var frameProcessor = new FrameProcessor(
            ProcessFrame,
            framePresenter,
            SessionStore,
            ballFinder,
            runtimeMetricsOptions);

        FrameLoop = new FrameProcessingLoop(
            frameFeed,
            frameProcessor,
            runtimeMetricsOptions);

        CommandHandler = new GameCommandHandler(
            StartGame,
            StopGame,
            cmdId => new GameEventPresenter(FrameLoop, runtimeState, videoDumpOrchestrator),
            cmdId => new GameEventPresenter(FrameLoop, runtimeState, videoDumpOrchestrator));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _ReplayCoordinator.Dispose();
        _VisionContextUpdatePublisher.Dispose();
    }

    public IGameSessionStore SessionStore { get; }

    public IStartGameInputPort StartGame { get; }

    public IStopGameInputPort StopGame { get; }

    public IProcessFrameInputPort ProcessFrame { get; }

    public ICompleteTableUpdateInputPort CompleteTableUpdate { get; }

    public ICompleteTableSceneUpdateInputPort CompleteTableSceneUpdate { get; }

    public FrameProcessingLoop FrameLoop { get; }

    public GameCommandHandler CommandHandler { get; }

    public async Task StopIfActive(CancellationToken ct)
    {
        if (!SessionStore.HasActive) return;

        GameEventPresenter presenter = new(FrameLoop, _RuntimeState, _VideoDumpOrchestrator, RecorderStateChangeReason.EndOfInput);
        await StopGame.Handle(new StopGameRequest(), presenter, ct);
    }
}
