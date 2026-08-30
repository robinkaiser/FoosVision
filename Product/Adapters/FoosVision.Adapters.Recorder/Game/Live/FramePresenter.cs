// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Game.Orchestration;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using FoosVision.UseCases.Game.ProcessFrame;

namespace FoosVision.Adapters.Recorder.Game.Live;

public class FramePresenter : IProcessFrameOutputPort
{
    private readonly IRecorderLiveDataPublisher _LiveDataPublisher;
    private readonly IRecorderLiveAnalysisPublisher _LiveAnalysisPublisher;
    private readonly IEncodedBallDetectionMaskProvider _BallDetectionMaskProvider;
    private readonly ICalibrationCoordinator _Calibration;
    private readonly IReplayCoordinator _Replay;
    private readonly bool _PublishObservations;
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
        _LiveDataPublisher = liveDataPublisher;
        _LiveAnalysisPublisher = liveAnalysisPublisher;
        _BallDetectionMaskProvider = ballDetectionMaskProvider;
        _Calibration = calibration;
        _Replay = replay;
        _PublishObservations = publishObservations;
        _PublishBallDetectionMask = publishBallDetectionMask;
    }

    public async Task ReportProcessed(ProcessFrameResponse r)
    {
        TrackingFrameMessage trackingFrame = new()
        {
            FrameId = r.Frame.Id,
            TimestampNs = r.Frame.TimestampNs,
            IsBallFound = r.IsBallFound,
            BallPosition = r.IsBallFound ? CreatePoint(r.BallPosition) : null,
            BallVelocityPxPerS = CreateVector(r.BallVelocityPxPerS),
            BallCandidates = [.. r.BallCandidates.Select(CreateBallCandidate)],
            Observations = _PublishObservations ? [.. r.Observations.Select(CreateObservation)] : [],
            Possession = CreatePossession(r.Possession),
            PossessionTimeMs = r.PossessionTimeMs,
            IsTimeFoul = r.IsTimeFoul,
        };

        await _LiveDataPublisher.PublishTrackingFrame(trackingFrame);

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
            await PublishBallDetectionMask(r);
        }
    }

    public Task ReportSkipped(string reason)
    {
        return Task.CompletedTask;
    }

    private static PointMessage CreatePoint(Point point)
        => new()
        {
            X = point.X,
            Y = point.Y,
        };

    private static VectorMessage CreateVector(Vector2 vector)
        => new()
        {
            X = vector.X,
            Y = vector.Y,
        };

    private static ObservedBallMessage CreateObservation(ObservedBall observedBall)
        => new()
        {
            Position = CreatePoint(observedBall.Position),
            QualityLevel = observedBall.QualityLevel switch
            {
                ObservationQualityLevel.VeryHighQuality => ObservationQualityLevelMessage.VeryHighQuality,
                ObservationQualityLevel.HighQuality => ObservationQualityLevelMessage.HighQuality,
                ObservationQualityLevel.LowQuality => ObservationQualityLevelMessage.LowQuality,
                _ => ObservationQualityLevelMessage.BelowMinimum,
            },
        };

    private static TrackedBallCandidateMessage CreateBallCandidate(TrackedBall trackedBall)
        => new()
        {
            Position = CreatePoint(trackedBall.Position),
            Status = trackedBall.Status switch
            {
                TrackingStatus.Predicted => TrackingStatusMessage.Predicted,
                _ => TrackingStatusMessage.Observed,
            },
            Confidence = trackedBall.Confidence switch
            {
                TrackingConfidence.High => TrackingConfidenceMessage.High,
                TrackingConfidence.Average => TrackingConfidenceMessage.Average,
                _ => TrackingConfidenceMessage.Low,
            },
            Evidence = trackedBall.Evidence switch
            {
                TrackingEvidence.VeryHighQualityObservation => TrackingEvidenceMessage.VeryHighQualityObservation,
                TrackingEvidence.HighQualityObservation => TrackingEvidenceMessage.HighQualityObservation,
                TrackingEvidence.LowQualityObservation => TrackingEvidenceMessage.LowQualityObservation,
                _ => TrackingEvidenceMessage.Prediction,
            },
            UnobservedAgeMs = trackedBall.UnobservedAgeMs,
        };

    private static PossessionMessage CreatePossession(BallPossession possession)
        => new()
        {
            Team = possession.Team switch
            {
                Team.A => TeamMessage.A,
                Team.B => TeamMessage.B,
                _ => TeamMessage.None,
            },
            Area = possession.Area switch
            {
                PossessionArea.Defense => PossessionAreaMessage.Defense,
                PossessionArea.FiveBar => PossessionAreaMessage.FiveBar,
                PossessionArea.ThreeBar => PossessionAreaMessage.ThreeBar,
                _ => PossessionAreaMessage.None,
            },
        };

    private async Task PublishBallDetectionMask(ProcessFrameResponse response)
    {
        _BallDetectionMaskProvider.GetEncodedBallDetectionMask(out EncodedBallDetectionMask mask);

        BallDetectionMaskMessage message = new()
        {
            FrameId = response.Frame.Id,
            TimestampNs = response.Frame.TimestampNs,
            Width = mask.Width,
            Height = mask.Height,
            Buffer = mask.Buffer,
            Length = mask.Length,
        };

        await _LiveAnalysisPublisher.PublishBallDetectionMask(message);
    }
}
