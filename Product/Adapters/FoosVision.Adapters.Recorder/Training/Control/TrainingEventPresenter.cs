// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Common.Live;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Protocol.Messages.Events;
using FoosVision.UseCases.Training.StartTraining;
using FoosVision.UseCases.Training.StopTraining;

namespace FoosVision.Adapters.Recorder.Training.Control;

public class TrainingEventPresenter :
    IStartTrainingOutputPort,
    IStopTrainingOutputPort
{
    private readonly ITrainingAnnouncements _Announcements;
    private readonly FrameProcessingLoop _FrameLoop;
    private readonly RecorderRuntimeStateController _RuntimeState;
    private readonly IVideoDumpOrchestrator _VideoDumpOrchestrator;
    private readonly RecorderStateChangeReason _StopReason;

    public TrainingEventPresenter(
        FrameProcessingLoop frameLoop,
        RecorderRuntimeStateController runtimeState,
        IVideoDumpOrchestrator videoDumpOrchestrator,
        ITrainingAnnouncements announcements,
        RecorderStateChangeReason stopReason = RecorderStateChangeReason.CommandCompleted)
    {
        _Announcements = announcements;
        _FrameLoop = frameLoop;
        _RuntimeState = runtimeState;
        _VideoDumpOrchestrator = videoDumpOrchestrator;
        _StopReason = stopReason;
    }

    public async Task ReportStarted(StartTrainingResponse response)
    {
        _Announcements.StartSession(response.SessionId);
        _FrameLoop.Start();

        await _RuntimeState.PublishIfChanged(
            RecorderRuntimeMode.TrainingRunning,
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
            CancellationToken.None, onlyWhenInactive: true);
    }

    public async Task ReportStopped(StopTrainingResponse response)
    {
        _Announcements.StopSession(response.SessionId);
        _FrameLoop.Stop();

        await _RuntimeState.PublishIfChanged(
            RecorderRuntimeMode.Idle,
            null,
            _StopReason,
            string.Empty,
            CancellationToken.None);

        _VideoDumpOrchestrator.TryScheduleDump(VideoDumpSessionKind.Training);
    }

    public async Task ReportStopFailed(string reason)
    {
        await _RuntimeState.PublishIfChanged(
            RecorderRuntimeMode.Faulted,
            null,
            RecorderStateChangeReason.InternalError,
            reason,
            CancellationToken.None, onlyWhenInactive: true);
    }
}
