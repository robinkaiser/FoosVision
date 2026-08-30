// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Protocol.Messages.Events;
using FoosVision.UseCases.Installation.StartInstall;
using FoosVision.UseCases.Installation.StopInstall;

namespace FoosVision.Adapters.Recorder.Installation.Control;

public class InstallEventPresenter :
    IStartInstallOutputPort,
    IStopInstallOutputPort
{
    private readonly FrameProcessingLoop _FrameLoop;
    private readonly RecorderRuntimeStateController _RuntimeState;
    private readonly IVideoDumpOrchestrator _VideoDumpOrchestrator;
    private readonly RecorderStateChangeReason _StopReason;

    public InstallEventPresenter(
        FrameProcessingLoop frameLoop,
        RecorderRuntimeStateController runtimeState,
        IVideoDumpOrchestrator videoDumpOrchestrator,
        RecorderStateChangeReason stopReason = RecorderStateChangeReason.CommandCompleted)
    {
        _FrameLoop = frameLoop;
        _RuntimeState = runtimeState;
        _VideoDumpOrchestrator = videoDumpOrchestrator;
        _StopReason = stopReason;
    }

    public async Task ReportStarted(StartInstallResponse response)
    {
        _FrameLoop.Start();

        await _RuntimeState.PublishIfChanged(
            RecorderRuntimeMode.InstallRunning,
            response.SessionId,
            RecorderStateChangeReason.CommandCompleted,
            string.Empty,
            CancellationToken.None);
    }

    public async Task ReportStartFailed(string reason)
    {
        await _RuntimeState.PublishIfChanged(
            RecorderRuntimeMode.Faulted,
            null,
            RecorderStateChangeReason.InternalError,
            reason,
            CancellationToken.None);
    }

    public async Task ReportStopped(StopInstallResponse response)
    {
        _FrameLoop.Stop();

        await _RuntimeState.PublishIfChanged(
            RecorderRuntimeMode.Idle,
            null,
            _StopReason,
            string.Empty,
            CancellationToken.None);

        _VideoDumpOrchestrator.TryScheduleDump(VideoDumpSessionKind.Installation);
    }

    public async Task ReportStopFailed(string reason)
    {
        await _RuntimeState.PublishIfChanged(
            RecorderRuntimeMode.Faulted,
            null,
            RecorderStateChangeReason.InternalError,
            reason,
            CancellationToken.None);
    }
}
