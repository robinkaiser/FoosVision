// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Calibration;
using FoosVision.Adapters.Recorder.Replay;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Adapters.Recorder.Training.Live;
using FoosVision.Adapters.Recorder.Training;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using FoosVision.UseCases.Training.ProcessFrame;
using FoosVision.UseCases.Training;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Training;

public class FramePresenterTests
{
    private readonly IReplayCoordinator _Replay = Substitute.For<IReplayCoordinator>();
    private readonly Guid _SessionId = Guid.NewGuid();
    private readonly IRecorderLiveDataPublisher _LiveDataPublisher = Substitute.For<IRecorderLiveDataPublisher>();
    private readonly IRecorderLiveAnalysisPublisher _LiveAnalysisPublisher = Substitute.For<IRecorderLiveAnalysisPublisher>();
    private readonly IEncodedBallDetectionMaskProvider _MaskProvider = Substitute.For<IEncodedBallDetectionMaskProvider>();
    private readonly ICalibrationCoordinator _Calibration = Substitute.For<ICalibrationCoordinator>();
    private readonly ITrainingAnnouncements _Announcements = Substitute.For<ITrainingAnnouncements>();

    [Fact]
    public async Task ReportProcessed_publishes_live_tracking_and_replay_return_and_hands_off_announcements()
    {
        FramePresenter testee = CreateTestee();
        ProcessFrameResponse r = CreateResponse();

        await testee.ReportProcessed(r);
        Received.InOrder(() =>
        {
            _Announcements.Invalidate(r.AnnouncementInvalidation!);
            _Announcements.Request(r.Announcement!);
        });
        await _LiveDataPublisher.Received(1).PublishTrackingFrame(Arg.Is<TrackingFrameMessage>(m =>
            m.FrameId == r.Tracking.Frame.Id && m.IsBallFound && !m.IsTimeFoul && m.Observations.Count == 0), Arg.Any<CancellationToken>());
        await _Calibration.Received(1).RequestTableUpdate(r.Tracking.Frame);
        await _Calibration.Received(1).RequestTableSceneUpdate(r.Tracking.Frame, Arg.Is<Option<Point>>(p => p.HasValue && p.Value == r.Tracking.BallPosition));
        await _Replay.Received(1).RequestReturnToLive(_SessionId, new Frame(9, 900_000_000));
        await _Replay.DidNotReceive().RequestReplay(Arg.Any<Guid>(), Arg.Any<Frame>(), Arg.Any<ReplayPlan>());
        await _LiveAnalysisPublisher.DidNotReceive().PublishReplayClip(Arg.Any<ReplayClipMessage>(), Arg.Any<CancellationToken>());
        _MaskProvider.DidNotReceive().GetEncodedBallDetectionMask(out Arg.Any<EncodedBallDetectionMask>());
    }

    [Fact]
    public async Task ReportProcessed_publishes_observations_when_enabled()
    {
        FramePresenter testee = CreateTestee(publishObservations: true);

        await testee.ReportProcessed(CreateResponse());

        await _LiveDataPublisher.Received(1).PublishTrackingFrame(Arg.Is<TrackingFrameMessage>(m => m.Observations.Count == 1), Arg.Any<CancellationToken>());
        _MaskProvider.DidNotReceive().GetEncodedBallDetectionMask(out Arg.Any<EncodedBallDetectionMask>());
    }

    [Fact]
    public async Task ReportProcessed_publishes_ball_detection_mask_when_enabled()
    {
        FramePresenter testee = CreateTestee(publishBallDetectionMask: true);

        await testee.ReportProcessed(CreateResponse());

        await _LiveDataPublisher.Received(1).PublishTrackingFrame(Arg.Is<TrackingFrameMessage>(m => m.Observations.Count == 0), Arg.Any<CancellationToken>());
        _MaskProvider.Received(1).GetEncodedBallDetectionMask(out Arg.Any<EncodedBallDetectionMask>());
        await _LiveAnalysisPublisher.Received(1).PublishBallDetectionMask(Arg.Is<BallDetectionMaskMessage>(m => m.FrameId == 10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReportProcessed_hands_domain_replay_plan_to_shared_execution()
    {
        FramePresenter testee = CreateTestee();
        ProcessFrameResponse response = CreateResponse();
        ReplayPlan replay = new(response.Tracking.Frame, new(100, 200), BallPossession.None, 0,
            850_000_000, 1_150_000_000, 0.25, 1);
        await testee.ReportProcessed(response with { Replay = replay, ReturnToLiveReplayFrame = null });
        await _Replay.Received(1).RequestReplay(_SessionId, response.Tracking.Frame, replay);
        await _Replay.DidNotReceive().RequestReturnToLive(Arg.Any<Guid>(), Arg.Any<Frame>());
    }

    private FramePresenter CreateTestee(
        bool publishObservations = false,
        bool publishBallDetectionMask = false)
    {
        return new FramePresenter(
            _LiveDataPublisher,
            _Calibration,
            _Replay,
            _LiveAnalysisPublisher,
            _MaskProvider,
            _Announcements,
            publishObservations,
            publishBallDetectionMask);
    }

    private ProcessFrameResponse CreateResponse()
    {
        Frame frame = new(10, 1_000_000_000);
        TrainingAttempt attempt = new(
            SessionId: _SessionId,
            AttemptId: 1,
            Strategy: TrainingStrategyKind.SnakeShot,
            Team: Team.A,
            Bar: default,
            Anchor: new TrainingAnchor(frame, new Point(100, 200), BallPossession.None));

        return new ProcessFrameResponse(
            SessionId: _SessionId,
            Tracking: new TrackingFrameResponse(
                Frame: frame,
                IsBallFound: true,
                BallPosition: new Point(100, 200),
                BallVelocityPxPerS: Vector2.Zero,
                BallCandidates: [],
                Observations: [new ObservedBall(new Point(100, 200), 0.9)],
                Possession: BallPossession.None,
                LastKnownPossession: BallPossession.None,
                PossessionTimeMs: 100,
                TrackingPhase: LiveTrackingPhase.Live,
                LastObservedBallAgeMs: 0,
                ClosestBar: null,
                RequestTableConfigUpdate: true,
                RequestTableSceneUpdate: true),
            Status: new TrainingStatusResponse(TrainingState.Waiting, attempt, null),
            PreparationConfirmed: attempt,
            Announcement: new TrainingAnnouncementRequest(attempt, 2, TrainingAnnouncementKind.Ready, "Ready", null),
            AnnouncementInvalidation: new TrainingAnnouncementInvalidation(attempt, 1),
            CompletedAttempt: null,
            AbortedAttempt: null,
            ReturnToLiveReplayFrame: new Frame(9, 900_000_000));
    }
}
