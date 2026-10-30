// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Calibration;
using FoosVision.Adapters.Recorder.Game.Orchestration;
using FoosVision.Adapters.Recorder.Live;
using FoosVision.Common.Types;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.UseCases.Game.ProcessFrame;

namespace FoosVision.Adapters.Recorder.Game.Live;

public class FramePresenter : IProcessFrameOutputPort
{
    private readonly LiveFramePublisher _Live;
    private readonly ICalibrationCoordinator _Calibration;
    private readonly IReplayCoordinator _Replay;
    private readonly bool _PublishBallDetectionMask;

    public FramePresenter(
        IRecorderLiveDataPublisher liveDataPublisher,
        ICalibrationCoordinator calibration,
        IReplayCoordinator replay,
        IRecorderLiveAnalysisPublisher liveAnalysisPublisher,
        IEncodedBallDetectionMaskProvider ballDetectionMaskProvider,
        bool publishObservations = false,
        bool publishBallDetectionMask = false)
    {
        _Live = new(liveDataPublisher, liveAnalysisPublisher, ballDetectionMaskProvider, publishObservations);
        _Calibration = calibration;
        _Replay = replay;
        _PublishBallDetectionMask = publishBallDetectionMask;
    }

    public async Task ReportProcessed(ProcessFrameResponse r)
    {
        await _Live.PublishTracking(new LiveFrame(
            r.Frame, r.IsBallFound, r.BallPosition, r.BallVelocityPxPerS,
            r.BallCandidates, r.Observations, r.Possession, r.PossessionTimeMs), r.IsTimeFoul);

        if (r.RequestTableConfigUpdate)
        {
            await _Calibration.RequestTableUpdate(r.Frame);
        }

        if (r.RequestTableSceneUpdate)
        {
            Option<Point> ballPosition = r.IsBallFound ?
                r.BallPosition :
                Option<Point>.None();

            await _Calibration.RequestTableSceneUpdate(r.Frame, ballPosition);
        }

        if (r.RequestReplay)
        {
            await _Replay.RequestReplay(
                r.Frame,
                r.ReplayAnchorFrame,
                r.ReplayAnchorPosition,
                r.ReplayAnchorPossession,
                r.ReplayAnchorPossessionTimeMs,
                r.ReplayTriggerKind);
        }

        if (_PublishBallDetectionMask)
        {
            await _Live.PublishMask(r.Frame);
        }
    }

    public Task ReportSkipped(string reason)
    {
        return Task.CompletedTask;
    }
}
