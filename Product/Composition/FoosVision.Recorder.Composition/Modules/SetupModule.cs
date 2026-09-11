// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Adapters.Recorder.Setup.Control;
using FoosVision.Adapters.Recorder.Setup.Live;
using FoosVision.Adapters.Recorder.Setup.Orchestration;
using FoosVision.Common.Metrics;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Recorder.Composition.InMemoryStores;
using FoosVision.UseCases.Calibration.Ports;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Setup.CompleteTableUpdate;
using FoosVision.UseCases.Setup.Ports;
using FoosVision.UseCases.Setup.ProcessFrame;
using FoosVision.UseCases.Setup.StartSetup;
using FoosVision.UseCases.Setup.StopSetup;

namespace FoosVision.Recorder.Composition.Modules;

internal class SetupModule : IDisposable
{
    private readonly IRecorderEventPublisher _EventPublisher;
    private readonly RecorderRuntimeStateController _RuntimeState;
    private readonly IVideoDumpOrchestrator _VideoDumpOrchestrator;
    private readonly FrameProcessingRatePublisher _FrameProcessingRatePublisher;

    public SetupModule(
        IFrameSource frameSource,
        IFrameFeed frameFeed,
        ITableConfigFinder tableConfigFinder,
        ITableConfigStore tableConfigStore,
        IRecorderEventPublisher eventPublisher,
        RecorderRuntimeStateController runtimeState,
        IRecorderLiveDataPublisher liveDataPublisher,
        IVideoDumpOrchestrator videoDumpOrchestrator,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        _EventPublisher = eventPublisher;
        _RuntimeState = runtimeState;
        _VideoDumpOrchestrator = videoDumpOrchestrator;
        SessionStore = new SetupSessionStore();

        StartSetup = new StartSetupInteractor(SessionStore, frameSource);
        StopSetup = new StopSetupInteractor(SessionStore, frameSource);
        ProcessFrame = new ProcessFrameInteractor(SessionStore);
        CompleteTableUpdate = new CompleteTableUpdateInteractor(SessionStore);

        var updateTable = new UpdateTableInteractor(tableConfigStore);
        var tableUpdatePresenter = new TableUpdatePresenter(liveDataPublisher);

        var calibration = new CalibrationCoordinator(
            updateTable,
            tableUpdatePresenter,
            tableConfigFinder,
            frameFeed,
            CompleteTableUpdate);

        var framePresenter = new FramePresenter(calibration);
        var frameProcessor = new FrameProcessor(ProcessFrame, framePresenter, SessionStore);

        FrameLoop = new FrameProcessingLoop(frameFeed, frameProcessor, runtimeMetricsOptions);
        _FrameProcessingRatePublisher = new FrameProcessingRatePublisher(FrameLoop, liveDataPublisher);

        CommandHandler = new SetupCommandHandler(
            StartSetup,
            StopSetup,
            cmdId => new SetupEventPresenter(FrameLoop, runtimeState, videoDumpOrchestrator),
            cmdId => new SetupEventPresenter(FrameLoop, runtimeState, videoDumpOrchestrator));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _FrameProcessingRatePublisher.Dispose();
    }

    public ISetupSessionStore SessionStore { get; }

    public IStartSetupInputPort StartSetup { get; }

    public IStopSetupInputPort StopSetup { get; }

    public IProcessFrameInputPort ProcessFrame { get; }

    public ICompleteTableUpdateInputPort CompleteTableUpdate { get; }

    public FrameProcessingLoop FrameLoop { get; }

    public SetupCommandHandler CommandHandler { get; }

    public async Task StopIfActive(CancellationToken ct)
    {
        if (!SessionStore.HasActive) return;

        SetupEventPresenter presenter = new(FrameLoop, _RuntimeState, _VideoDumpOrchestrator, RecorderStateChangeReason.EndOfInput);
        await StopSetup.Handle(new StopSetupRequest(), presenter, ct);
    }
}
