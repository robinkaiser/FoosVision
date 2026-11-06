// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Calibration;
using FoosVision.Adapters.Recorder.Live;
using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.LiveAnalysis;
using FoosVision.UseCases.Training.ProcessFrame;

namespace FoosVision.Adapters.Recorder.Training.Live;

public class FramePresenter : IProcessFrameOutputPort
{
    private static readonly Source _Log = new("Recorder.Training");
    private readonly LiveFramePublisher _Live;
    private readonly ICalibrationCoordinator _Calibration;
    private readonly IRecorderLiveAnalysisPublisher _LiveAnalysisPublisher;
    private readonly ITrainingAnnouncements _Announcements;
    private readonly bool _PublishBallDetectionMask;
    private TrainingStatusResponse? _LastStatus;

    public FramePresenter(
        IRecorderLiveDataPublisher liveDataPublisher,
        ICalibrationCoordinator calibration,
        IRecorderLiveAnalysisPublisher liveAnalysisPublisher,
        IEncodedBallDetectionMaskProvider ballDetectionMaskProvider,
        ITrainingAnnouncements announcements,
        bool publishObservations = false,
        bool publishBallDetectionMask = false)
    {
        _Live = new(liveDataPublisher, liveAnalysisPublisher, ballDetectionMaskProvider, publishObservations);
        _Calibration = calibration;
        _LiveAnalysisPublisher = liveAnalysisPublisher;
        _Announcements = announcements;
        _PublishBallDetectionMask = publishBallDetectionMask;
    }

    public async Task ReportProcessed(ProcessFrameResponse response)
    {
        // Local announcements must not depend on transport publication succeeding.
        PresentTrainingOutputs(response);

        TrackingFrameResponse r = response.Tracking;
        await _Live.PublishTracking(new LiveFrame(
            r.Frame, r.IsBallFound, r.BallPosition, r.BallVelocityPxPerS,
            r.BallCandidates, r.Observations, r.Possession, r.PossessionTimeMs));

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

        if (response.ReturnToLiveReplayFrame is Frame triggerFrame)
        {
            await _LiveAnalysisPublisher.PublishReplayReturnToLive(new ReplayReturnToLiveMessage
            {
                SessionId = response.SessionId,
                TriggerFrameId = triggerFrame.Id,
                TriggerTimestampNs = triggerFrame.TimestampNs,
            });
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

    private void PresentTrainingOutputs(ProcessFrameResponse response)
    {
        if (response.AnnouncementInvalidation is { } invalidation)
        {
            _Announcements.Invalidate(invalidation);
        }

        if (response.Announcement is { } announcement)
        {
            _Announcements.Request(announcement);
        }

        if (_LastStatus?.State != response.Status.State ||
            _LastStatus?.Attempt?.SessionId != response.Status.Attempt?.SessionId ||
            _LastStatus?.Attempt?.AttemptId != response.Status.Attempt?.AttemptId ||
            _LastStatus?.Variant != response.Status.Variant)
        {
            _LastStatus = response.Status;
            _Log.Information("Training state changed. SessionId={SessionId} State={State} Attempt={Attempt}",
                response.SessionId, response.Status.State, response.Status.Attempt);
        }
    }
}
