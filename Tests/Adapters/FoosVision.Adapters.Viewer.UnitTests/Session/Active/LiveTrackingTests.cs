// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Protocol.Messages.Live;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class LiveTrackingTests
{
    [Fact]
    public void Handle_projects_messages_into_overlay_updates()
    {
        LiveTrackingPresenter sut = CreateSut(out RecordingOverlaySink overlaySink);

        sut.Handle(new TrackingFrameMessage
        {
            FrameId = 12,
            TimestampNs = 3_000_000_000,
            IsBallFound = true,
            BallPosition = new PointMessage { X = 960, Y = 540 },
            Observations =
            [
                new ObservedBallMessage
                {
                    Position = new PointMessage { X = 100, Y = 200 },
                    QualityLevel = ObservationQualityLevelMessage.HighQuality,
                },
            ],
            Possession = new PossessionMessage
            {
                Team = TeamMessage.A,
                Area = PossessionAreaMessage.FiveBar,
            },
            PossessionTimeMs = 1234,
            IsTimeFoul = false,
        });

        TrackingOverlayState state = Assert.Single(overlaySink.TrackingStates);
        Assert.Equal(Team.A, state.PossessingTeam);
        Assert.Equal(PossessionArea.FiveBar, state.PossessionArea);
        Assert.Single(state.Trail);
        Assert.NotNull(state.BallPosition);
        ObservationOverlayPoint observation = Assert.Single(state.Observations);
        Assert.Equal(ObservationQualityLevel.HighQuality, observation.QualityLevel);
    }

    [Fact]
    public void Handle_projects_observations_when_observations_are_present()
    {
        LiveTrackingPresenter sut = CreateSut(out RecordingOverlaySink overlaySink);

        sut.Handle(new TrackingFrameMessage
        {
            FrameId = 12,
            TimestampNs = 3_000_000_000,
            IsBallFound = true,
            BallPosition = new PointMessage { X = 960, Y = 540 },
            Observations =
            [
                new ObservedBallMessage
                {
                    Position = new PointMessage { X = 480, Y = 270 },
                    QualityLevel = ObservationQualityLevelMessage.LowQuality,
                },
                new ObservedBallMessage
                {
                    Position = new PointMessage { X = 1440, Y = 810 },
                    QualityLevel = ObservationQualityLevelMessage.VeryHighQuality,
                },
            ],
        });

        TrackingOverlayState state = Assert.Single(overlaySink.TrackingStates);
        Assert.Equal(
            [
                new ObservationOverlayPoint(new OverlayPoint(0.25f, 0.25f), ObservationQualityLevel.LowQuality),
                new ObservationOverlayPoint(new OverlayPoint(0.75f, 0.75f), ObservationQualityLevel.VeryHighQuality),
            ],
            state.Observations);
    }

    [Fact]
    public void Handle_projects_ball_candidates_with_status_and_confidence()
    {
        LiveTrackingPresenter sut = CreateSut(out RecordingOverlaySink overlaySink);

        sut.Handle(new TrackingFrameMessage
        {
            FrameId = 12,
            TimestampNs = 3_000_000_000,
            IsBallFound = false,
            BallCandidates =
            [
                new TrackedBallCandidateMessage
                {
                    Position = new PointMessage { X = 480, Y = 270 },
                    Status = TrackingStatusMessage.Predicted,
                    Confidence = TrackingConfidenceMessage.Low,
                },
                new TrackedBallCandidateMessage
                {
                    Position = new PointMessage { X = 1440, Y = 810 },
                    Status = TrackingStatusMessage.Observed,
                    Confidence = TrackingConfidenceMessage.High,
                },
            ],
        });

        TrackingOverlayState state = Assert.Single(overlaySink.TrackingStates);
        Assert.Null(state.BallPosition);
        Assert.Equal(
            [
                new BallCandidateOverlayPoint(new OverlayPoint(0.25f, 0.25f), TrackingStatus.Predicted, TrackingConfidence.Low),
                new BallCandidateOverlayPoint(new OverlayPoint(0.75f, 0.75f), TrackingStatus.Observed, TrackingConfidence.High),
            ],
            state.BallCandidates);
        Assert.Empty(state.Trail);
    }

    [Fact]
    public void Handle_reports_live_speed_metrics_after_table_update()
    {
        LiveTrackingPresenter sut = CreateSut(out RecordingOverlaySink overlaySink);
        TableUpdateMessage tableUpdate = CreateTableUpdateMessage();

        Assert.True(TableConfigurationMessageMapper.TryMap(tableUpdate.TableConfiguration, out TableConfiguration tableConfiguration));
        sut.UpdateTableConfiguration(tableConfiguration);
        sut.Handle(new TrackingFrameMessage
        {
            FrameId = 12,
            TimestampNs = 3_000_000_000,
            IsBallFound = true,
            BallPosition = new PointMessage { X = 960, Y = 540 },
            BallVelocityPxPerS = new VectorMessage { X = -1000, Y = 2000 },
        });

        TableImageScale scale = TableImageScale.From(tableConfiguration);
        TrackingOverlayState state = overlaySink.TrackingStates[^1];

        Assert.Collection(
            state.Metrics,
            metric =>
            {
                Assert.Equal(ReplayAnalyzer.GoalSpeedMetricName, metric.Name);
                Assert.Equal(scale.ConvertGoalAxisSpeedPxPerSToKmh(1000), metric.Value, precision: 3);
                Assert.Equal("km/h", metric.Unit);
            },
            metric =>
            {
                Assert.Equal(ReplayAnalyzer.SideSpeedMetricName, metric.Name);
                Assert.Equal(scale.ConvertSideAxisSpeedPxPerSToKmh(2000), metric.Value, precision: 3);
                Assert.Equal("km/h", metric.Unit);
            });
    }

    [Fact]
    public async Task Handle_reports_live_ball_to_replay_observer_when_replay_is_active()
    {
        List<Point?> liveBallPositions = [];
        LiveTrackingPresenter sut = CreateSut(
            out RecordingOverlaySink overlaySink,
            hasActiveReplay: () => true,
            observeLiveTracking: position =>
            {
                liveBallPositions.Add(position);
                return Task.CompletedTask;
            });

        sut.Handle(CreateTrackingFrame(12, 3_000_000_000, isBallFound: true, new PointMessage { X = 100, Y = 200 }));
        await Task.Yield();

        Assert.Empty(overlaySink.TrackingStates);
        Point position = Assert.IsType<Point>(Assert.Single(liveBallPositions));
        Assert.Equal(100, position.X);
        Assert.Equal(200, position.Y);
    }

    private static LiveTrackingPresenter CreateSut(
        out RecordingOverlaySink overlaySink,
        Func<bool>? isReplayPending = null,
        Func<bool>? hasActiveReplay = null,
        Func<Point?, Task>? observeLiveTracking = null)
    {
        List<string> events = [];
        overlaySink = new RecordingOverlaySink(events);

        return new LiveTrackingPresenter(
            overlaySink,
            new TrackingOverlayProjector(),
            () => DateTimeOffset.UtcNow,
            isReplayPending ?? (() => false),
            hasActiveReplay ?? (() => false),
            observeLiveTracking ?? (_ => Task.CompletedTask));
    }
}
