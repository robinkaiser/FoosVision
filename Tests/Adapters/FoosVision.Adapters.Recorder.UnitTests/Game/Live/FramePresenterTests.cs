// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Game.Live;
using FoosVision.Adapters.Recorder.Game.Orchestration;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using FoosVision.UseCases.Game.ProcessFrame;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Game.Live;

public class FramePresenterTests
{
    private readonly RecordingLiveDataPublisher _LiveDataPublisher;
    private readonly RecordingLiveAnalysisPublisher _LiveAnalysisPublisher;
    private readonly FakeBallDetectionMaskProvider _MaskProvider;
    private readonly byte[] _EncodedMask;

    public FramePresenterTests()
    {
        _LiveDataPublisher = new();
        _LiveAnalysisPublisher = new();

        _EncodedMask =
        [
          4, 2, 129, 1, 129, 3, 132, 127, 133,
        ];

        _MaskProvider = new(4, 4, _EncodedMask);
    }

    [Fact]
    public async Task ReportProcessed_publishes_ball_candidates_by_default()
    {
        FramePresenter testee = CreateTestee();

        await testee.ReportProcessed(CreateResponse());

        Assert.NotNull(_LiveDataPublisher.TrackingFrame);
        TrackedBallCandidateMessage candidate = Assert.Single(_LiveDataPublisher.TrackingFrame.BallCandidates);
        Assert.Equal(TrackingStatusMessage.Predicted, candidate.Status);
        Assert.Equal(TrackingEvidenceMessage.Prediction, candidate.Evidence);
    }

    [Fact]
    public async Task ReportProcessed_omits_observations_by_default()
    {
        FramePresenter testee = CreateTestee();

        await testee.ReportProcessed(CreateResponse());

        Assert.NotNull(_LiveDataPublisher.TrackingFrame);
        Assert.Empty(_LiveDataPublisher.TrackingFrame.Observations);
    }

    [Fact]
    public async Task ReportProcessed_publishes_observations_when_enabled()
    {
        FramePresenter testee = CreateTestee(publishObservations: true);

        await testee.ReportProcessed(CreateResponse());

        Assert.NotNull(_LiveDataPublisher.TrackingFrame);
        ObservedBallMessage observation = Assert.Single(_LiveDataPublisher.TrackingFrame.Observations);
        Assert.Equal(100, observation.Position.X);
        Assert.Equal(100, observation.Position.Y);
        Assert.Equal(ObservationQualityLevelMessage.VeryHighQuality, observation.QualityLevel);
    }

    [Fact]
    public async Task ReportProcessed_publishes_ball_detection_mask_when_enabled()
    {
        FramePresenter testee = CreateTestee(publishBallDetectionMask: true);

        await testee.ReportProcessed(CreateResponse());

        Assert.NotNull(_LiveAnalysisPublisher.BallDetectionMask);
        Assert.Equal(1UL, _LiveAnalysisPublisher.BallDetectionMask.FrameId);
        Assert.Equal(1_000_000_000, _LiveAnalysisPublisher.BallDetectionMask.TimestampNs);
        Assert.Equal(4, _LiveAnalysisPublisher.BallDetectionMask.Width);
        Assert.Equal(4, _LiveAnalysisPublisher.BallDetectionMask.Height);
        Assert.Same(_EncodedMask, _LiveAnalysisPublisher.BallDetectionMask.Buffer);
        Assert.Equal(_EncodedMask.Length, _LiveAnalysisPublisher.BallDetectionMask.Length);
    }

    private FramePresenter CreateTestee(
        bool publishObservations = false,
        bool publishBallDetectionMask = false)
    {
        return new FramePresenter(
            _LiveDataPublisher,
            Substitute.For<ICalibrationCoordinator>(),
            Substitute.For<IReplayCoordinator>(),
            _LiveAnalysisPublisher,
            _MaskProvider,
            publishObservations,
            publishBallDetectionMask);
    }

    private static ProcessFrameResponse CreateResponse()
    {
        return new ProcessFrameResponse(
            Frame: new Frame(1, 1_000_000_000),
            IsBallFound: true,
            BallPosition: new Point(960, 540),
            BallVelocityPxPerS: Vector2.Zero,
            BallCandidates:
            [
                new TrackedBall(
                    1,
                    new Frame(1, 1_000_000_000),
                    new Point(100, 100),
                    TrackingConfidence.Low,
                    TrackingStatus.Predicted,
                    Vector2.Zero)
                {
                    Evidence = TrackingEvidence.Prediction,
                    UnobservedAgeMs = 33,
                },
            ],
            Observations: [new ObservedBall(new Point(100, 100), 0.8)],
            Possession: BallPossession.None,
            PossessionTimeMs: 0,
            IsTimeFoul: false,
            RequestTableConfigUpdate: false,
            RequestTableSceneUpdate: false,
            RequestReplay: false,
            ReplayAnchorFrame: new Frame(1, 1_000_000_000),
            ReplayAnchorPosition: new Point(960, 540),
            ReplayAnchorPossession: BallPossession.None,
            ReplayAnchorPossessionTimeMs: 0,
            ReplayTriggerKind: ReplayTriggerKind.BallDisappeared);
    }

    private sealed class RecordingLiveDataPublisher : IRecorderLiveDataPublisher
    {
        public TrackingFrameMessage? TrackingFrame { get; private set; }

        public Task PublishTrackingFrame(TrackingFrameMessage frame, CancellationToken ct = default)
        {
            TrackingFrame = frame;
            return Task.CompletedTask;
        }

        public Task PublishTableUpdate(TableUpdateMessage update, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLiveAnalysisPublisher : IRecorderLiveAnalysisPublisher
    {
        public BallDetectionMaskMessage? BallDetectionMask { get; private set; }

        public Task PublishReplayStarted(ReplayStartedMessage replayStarted, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishReplay(ReplayMessage replay, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishVisionContext(VisionContextMessage visionContext, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishBallDetectionMask(BallDetectionMaskMessage ballDetectionMask, CancellationToken ct = default)
        {
            BallDetectionMask = ballDetectionMask;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeBallDetectionMaskProvider : IEncodedBallDetectionMaskProvider
    {
        private readonly EncodedBallDetectionMask _Mask;

        public FakeBallDetectionMaskProvider(int width, int height, byte[] buffer)
        {
            _Mask = new EncodedBallDetectionMask(buffer, buffer.Length, width, height);
        }

        public void GetEncodedBallDetectionMask(out EncodedBallDetectionMask mask)
        {
            mask = _Mask;
        }
    }
}
