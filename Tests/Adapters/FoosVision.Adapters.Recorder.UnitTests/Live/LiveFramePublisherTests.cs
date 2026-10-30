// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Live;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using NSubstitute;

namespace FoosVision.Adapters.Recorder.UnitTests.Live;

public class LiveFramePublisherTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Shared_tracking_publishes_neutral_facts_and_explicit_game_foul(bool foul)
    {
        IRecorderLiveDataPublisher data = Substitute.For<IRecorderLiveDataPublisher>();
        LiveFramePublisher publisher = new(data, Substitute.For<IRecorderLiveAnalysisPublisher>(),
            Substitute.For<IEncodedBallDetectionMaskProvider>(), publishObservations: true);
        Frame frame = new(17, 123);
        Point position = new(10, 20);
        TrackedBall ball = new(1, frame, position, TrackingConfidence.High, TrackingStatus.Observed, new Vector2(30, 40));
        LiveFrame facts = new(frame, true, position, ball.VelocityPxPerS, [ball], [],
            new BallPossession(Team.B, PossessionArea.FiveBar), 1200);

        await publisher.PublishTracking(facts, foul);

        await data.Received(1).PublishTrackingFrame(Arg.Is<TrackingFrameMessage>(m =>
            m.FrameId == 17 && m.TimestampNs == 123 && m.IsBallFound &&
            m.BallPosition!.X == 10 && m.BallPosition.Y == 20 &&
            m.BallVelocityPxPerS.X == 30 && m.BallVelocityPxPerS.Y == 40 &&
            m.BallCandidates.Count == 1 && m.Possession.Team == TeamMessage.B &&
            m.Possession.Area == PossessionAreaMessage.FiveBar && m.PossessionTimeMs == 1200 &&
            m.IsTimeFoul == foul), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Shared_publisher_can_publish_lost_ball_and_mask_without_game_response()
    {
        IRecorderLiveDataPublisher data = Substitute.For<IRecorderLiveDataPublisher>();
        IRecorderLiveAnalysisPublisher analysis = Substitute.For<IRecorderLiveAnalysisPublisher>();
        IEncodedBallDetectionMaskProvider masks = Substitute.For<IEncodedBallDetectionMaskProvider>();
        byte[] buffer = [1, 2];
        masks.When(m => m.GetEncodedBallDetectionMask(out Arg.Any<EncodedBallDetectionMask>())).Do(call =>
        {
            call[0] = new EncodedBallDetectionMask(buffer, 2, 2, 1);
        });
        LiveFramePublisher publisher = new(data, analysis, masks);
        Frame frame = new(18, 456);
        await publisher.PublishTracking(new(frame, false, default, default, [], [], BallPossession.None, 0));
        await publisher.PublishMask(frame);

        await data.Received(1).PublishTrackingFrame(Arg.Is<TrackingFrameMessage>(m =>
            !m.IsBallFound && m.BallPosition == null && !m.IsTimeFoul), Arg.Any<CancellationToken>());
        await analysis.Received(1).PublishBallDetectionMask(Arg.Is<BallDetectionMaskMessage>(m =>
            m.FrameId == 18 && m.TimestampNs == 456 && m.Buffer == buffer && m.Length == 2), Arg.Any<CancellationToken>());
    }
}
