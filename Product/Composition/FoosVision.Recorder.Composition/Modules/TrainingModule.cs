// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Adapters.Recorder.Calibration;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Adapters.Recorder.Live;
using FoosVision.Adapters.Recorder.Training.Control;
using FoosVision.Adapters.Recorder.Training.Live;
using FoosVision.Adapters.Recorder.Training;
using FoosVision.Common.Metrics;
using FoosVision.Domain.Training.Services;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Recorder.Composition.InMemoryStores;
using FoosVision.UseCases.Calibration.Ports;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Calibration.UpdateTableScene;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Training.CompleteTableSceneUpdate;
using FoosVision.UseCases.Training.CompleteTableUpdate;
using FoosVision.UseCases.Training.Ports;
using FoosVision.UseCases.Training.ProcessFrame;
using FoosVision.UseCases.Training.StartTraining;
using FoosVision.UseCases.Training.StopTraining;

namespace FoosVision.Recorder.Composition.Modules;

internal class TrainingModule : IDisposable
{
    private readonly RecorderRuntimeStateController _RuntimeState;
    private readonly ITrainingAnnouncements _Announcements;
    private readonly VisionContextUpdatePublisher _VisionContextUpdatePublisher;
    private readonly IVideoDumpOrchestrator _VideoDumpOrchestrator;
    private readonly FrameProcessingRatePublisher _FrameProcessingRatePublisher;

    public TrainingModule(
        IFrameSource frameSource,
        IFrameFeed frameFeed,
        IBallFinder ballFinder,
        IEncodedBallDetectionMaskProvider ballDetectionMaskProvider,
        ITableConfigFinder tableConfigFinder,
        ITableSceneUpdater tableSceneUpdater,
        IEncodedVisionContextProvider visionContextProvider,
        ISettingsStore settingsStore,
        ITableConfigStore tableConfigStore,
        RecorderRuntimeStateController runtimeState,
        IRecorderLiveDataPublisher liveDataPublisher,
        IRecorderLiveAnalysisPublisher liveAnalysisPublisher,
        IUpdateTableSceneOutputPort updateTableScenePresenter,
        IVideoDumpOrchestrator videoDumpOrchestrator,
        SessionGate sessionGate,
        ITrainingOptionsProvider optionsProvider,
        ITrainingAnnouncements? announcements,
        bool publishObservations = false,
        bool publishBallDetectionMask = false,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _RuntimeState = runtimeState;
        _VideoDumpOrchestrator = videoDumpOrchestrator;
        SessionStore = new TrainingSessionStore();
        _Announcements = announcements ?? new NullTrainingAnnouncements();

        StartTraining = new StartTrainingInteractor(SessionStore, settingsStore, frameSource, optionsProvider, new TrainingRandom(), sessionGate);
        StopTraining = new StopTrainingInteractor(SessionStore, frameSource, sessionGate);
        ProcessFrame = new ProcessFrameInteractor(SessionStore);
        CompleteTableUpdate = new CompleteTableUpdateInteractor(SessionStore);
        CompleteTableSceneUpdate = new CompleteTableSceneUpdateInteractor(SessionStore);

        var updateTable = new UpdateTableInteractor(tableConfigStore);
        var updateTableScene = new UpdateTableSceneInteractor(tableConfigStore);

        var calibration = new CalibrationCoordinator(
            updateTable,
            tableConfigFinder,
            updateTableScene,
            tableSceneUpdater,
            frameFeed,
            () =>
            {
                if (!SessionStore.LoadActive().TryGetValue(out var session)) return null;
                return new CalibrationSession(
                    () => SessionStore.LoadActive().TryGetValue(out var active) && active.Id == session.Id,
                    new TableUpdatePresenter(session, liveDataPublisher),
                    updateTableScenePresenter,
                    () => CompleteTableUpdate.Handle(new(session.Id), CancellationToken.None),
                    () => CompleteTableSceneUpdate.Handle(new(session.Id), CancellationToken.None));
            });

        _VisionContextUpdatePublisher = new VisionContextUpdatePublisher(
            visionContextProvider,
            liveAnalysisPublisher,
            () => SessionStore.HasActive);

        var framePresenter = new FramePresenter(
            liveDataPublisher,
            calibration,
            liveAnalysisPublisher,
            ballDetectionMaskProvider,
            _Announcements,
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
        _FrameProcessingRatePublisher = new FrameProcessingRatePublisher(FrameLoop, liveDataPublisher);

        CommandHandler = new TrainingCommandHandler(
            StartTraining,
            StopTraining,
            cmdId => new TrainingEventPresenter(FrameLoop, runtimeState, videoDumpOrchestrator, _Announcements),
            cmdId => new TrainingEventPresenter(FrameLoop, runtimeState, videoDumpOrchestrator, _Announcements));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        FrameLoop.Stop();
        _VisionContextUpdatePublisher.Dispose();
        _FrameProcessingRatePublisher.Dispose();
    }

    public ITrainingSessionStore SessionStore { get; }

    public IStartTrainingInputPort StartTraining { get; }

    public IStopTrainingInputPort StopTraining { get; }

    public IProcessFrameInputPort ProcessFrame { get; }

    public ICompleteTableUpdateInputPort CompleteTableUpdate { get; }

    public ICompleteTableSceneUpdateInputPort CompleteTableSceneUpdate { get; }

    public FrameProcessingLoop FrameLoop { get; }

    public TrainingCommandHandler CommandHandler { get; }

    public async Task StopIfActive(CancellationToken ct, RecorderStateChangeReason reason)
    {
        if (!SessionStore.HasActive) return;

        TrainingEventPresenter presenter = new(FrameLoop, _RuntimeState, _VideoDumpOrchestrator, _Announcements, reason);
        await StopTraining.Handle(new StopTrainingRequest(), presenter, ct);
    }

    private sealed class TrainingRandom : ITrainingRandom
    {
        public double NextUnit() => Random.Shared.NextDouble();
    }
}
