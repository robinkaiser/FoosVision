// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Adapters.Recorder.Installation.Control;
using FoosVision.Adapters.Recorder.Installation.Live;
using FoosVision.Adapters.Recorder.Installation.Orchestration;
using FoosVision.Common.Metrics;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Recorder.Composition.InMemoryStores;
using FoosVision.UseCases.Calibration.Ports;
using FoosVision.UseCases.Calibration.UpdateTable;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Installation.CompleteTableUpdate;
using FoosVision.UseCases.Installation.Ports;
using FoosVision.UseCases.Installation.ProcessFrame;
using FoosVision.UseCases.Installation.StartInstall;
using FoosVision.UseCases.Installation.StopInstall;

namespace FoosVision.Recorder.Composition.Modules;

internal class InstallationModule : IDisposable
{
    private readonly IRecorderEventPublisher _EventPublisher;
    private readonly RecorderRuntimeStateController _RuntimeState;
    private readonly IVideoDumpOrchestrator _VideoDumpOrchestrator;

    public InstallationModule(
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
        SessionStore = new InstallSessionStore();

        StartInstall = new StartInstallInteractor(SessionStore, frameSource);
        StopInstall = new StopInstallInteractor(SessionStore, frameSource);
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

        CommandHandler = new InstallCommandHandler(
            StartInstall,
            StopInstall,
            cmdId => new InstallEventPresenter(FrameLoop, runtimeState, videoDumpOrchestrator),
            cmdId => new InstallEventPresenter(FrameLoop, runtimeState, videoDumpOrchestrator));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public IInstallSessionStore SessionStore { get; }

    public IStartInstallInputPort StartInstall { get; }

    public IStopInstallInputPort StopInstall { get; }

    public IProcessFrameInputPort ProcessFrame { get; }

    public ICompleteTableUpdateInputPort CompleteTableUpdate { get; }

    public FrameProcessingLoop FrameLoop { get; }

    public InstallCommandHandler CommandHandler { get; }

    public async Task StopIfActive(CancellationToken ct)
    {
        if (!SessionStore.HasActive) return;

        InstallEventPresenter presenter = new(FrameLoop, _RuntimeState, _VideoDumpOrchestrator, RecorderStateChangeReason.EndOfInput);
        await StopInstall.Handle(new StopInstallRequest(), presenter, ct);
    }
}
