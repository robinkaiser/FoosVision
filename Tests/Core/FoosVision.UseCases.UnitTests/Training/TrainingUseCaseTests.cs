// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.Entities;
using FoosVision.Domain.Training.Events;
using FoosVision.Domain.Training.ValueObjects;
using FoosVision.Domain.UnitTests.Training;
using FoosVision.Domain.UnitTests;
using FoosVision.UseCases.Common;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Live.Ports;
using FoosVision.UseCases.Training.CompleteTableSceneUpdate;
using FoosVision.UseCases.Training.CompleteTableUpdate;
using FoosVision.UseCases.Training.Ports;
using FoosVision.UseCases.Training.ProcessFrame;
using FoosVision.UseCases.Training.StartTraining;
using FoosVision.UseCases.Training.StopTraining;
using FoosVision.UseCases.Training;
using NSubstitute;
using static FoosVision.Domain.UnitTests.Training.TrainingTestData;

namespace FoosVision.UseCases.UnitTests.Training;

public class TrainingUseCaseTests
{
    private readonly SessionGate _SessionGate = new();
    private readonly FakeTrainingSessionStore _Store = new();
    private readonly ISettingsStore _Settings = Substitute.For<ISettingsStore>();
    private readonly IFrameSource _Frames = Substitute.For<IFrameSource>();
    private readonly ITrainingOptionsProvider _Options = Substitute.For<ITrainingOptionsProvider>();
    private readonly IStartTrainingOutputPort _StartOutput = Substitute.For<IStartTrainingOutputPort>();
    private readonly StartTrainingInteractor _Start;

    public TrainingUseCaseTests()
    {
        _Settings.LoadTableConfig().Returns(Option<TableConfiguration>.Some(TableConfig.Config));
        _Options.LoadTrainingOptions().Returns(TrainingOptions.Default);
        _Frames.Configure(Arg.Any<CancellationToken>()).Returns(FrameSourceResult.Success);
        _Frames.Start(Arg.Any<CancellationToken>()).Returns(FrameSourceResult.Success);
        _Start = new(_Store, _Settings, _Frames, _Options, new CountingRandom(), _SessionGate);
    }

    [Fact]
    public async Task Start_requires_setup_and_never_configures_frames_without_it()
    {
        _Settings.LoadTableConfig().Returns(Option<TableConfiguration>.None());
        await _Start.Handle(new(), _StartOutput, TestContext.Current.CancellationToken);
        await _StartOutput.Received(1).ReportStartFailed(Arg.Any<string>());
        await _Frames.DidNotReceive().Configure(Arg.Any<CancellationToken>());
        Assert.False(_Store.HasActive);
    }

    [Fact]
    public async Task Duplicate_start_is_rejected_and_options_are_snapshotted_until_restart()
    {
        await _Start.Handle(new(), _StartOutput, TestContext.Current.CancellationToken);
        TrainingSession session = _Store.LoadActive().Value;
        Assert.Equal(3, session.Options.Strategies.Count);
        _Options.LoadTrainingOptions().Returns(new TrainingOptions([Option(TrainingStrategyKind.PullShot)]));
        await _Start.Handle(new(), _StartOutput, TestContext.Current.CancellationToken);
        Assert.Same(session, _Store.LoadActive().Value);
        Assert.Equal(3, session.Options.Strategies.Count);
        await _Frames.Received(1).Start(Arg.Any<CancellationToken>());
        await _StartOutput.Received(1).ReportStarted(Arg.Is<StartTrainingResponse>(response => response.SessionId == session.Id));
        await _StartOutput.Received(1).ReportStartFailed(Arg.Any<string>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Start_failure_releases_partial_frame_source_and_session(bool configureFails)
    {
        if (configureFails)
        {
            _Frames.Configure(Arg.Any<CancellationToken>()).Returns(FrameSourceResult.Failure);
        }
        else
        {
            _Frames.Start(Arg.Any<CancellationToken>()).Returns(FrameSourceResult.Failure);
        }

        await _Start.Handle(new(), _StartOutput, TestContext.Current.CancellationToken);
        Assert.False(_Store.HasActive);
        await _Frames.Received(1).Stop(CancellationToken.None);
        await _StartOutput.Received(1).ReportStartFailed(Arg.Any<string>());
        await _StartOutput.DidNotReceive().ReportStarted(Arg.Any<StartTrainingResponse>());
    }

    [Fact]
    public async Task Start_presenter_exception_cleans_source_session_and_reservation()
    {
        _StartOutput.ReportStarted(Arg.Any<StartTrainingResponse>()).Returns<Task>(_ => throw new InvalidOperationException("presenter"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _Start.Handle(new(), _StartOutput, CancellationToken.None));
        Assert.False(_Store.HasActive);
        await _Frames.Received(1).Stop(CancellationToken.None);
        using SessionGate.Reservation? next = _SessionGate.TryReserve();
        Assert.NotNull(next);
    }

    [Fact]
    public async Task Stop_ends_domain_session_before_waiting_for_source_and_holds_reservation_until_cleanup()
    {
        await _Start.Handle(new(), _StartOutput, TestContext.Current.CancellationToken);
        TrainingSession session = _Store.LoadActive().Value;
        TaskCompletionSource sourceStopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _Frames.Stop(Arg.Any<CancellationToken>()).Returns(sourceStopped.Task);
        IStopTrainingOutputPort output = Substitute.For<IStopTrainingOutputPort>();
        StopTrainingInteractor stop = new(_Store, _Frames, _SessionGate);

        Task stopping = stop.Handle(new(), output, TestContext.Current.CancellationToken);

        Assert.False(stopping.IsCompleted);
        Assert.Equal(TrainingState.Stopped, session.State);
        Assert.True(_Store.HasActive);
        Assert.Null(_SessionGate.TryReserve());
        await output.DidNotReceive().ReportStopped(Arg.Any<StopTrainingResponse>());

        sourceStopped.SetResult();
        await stopping;

        Assert.False(_Store.HasActive);
        await output.Received(1).ReportStopped(new StopTrainingResponse(session.Id));
        using SessionGate.Reservation? next = _SessionGate.TryReserve();
        Assert.NotNull(next);
    }

    [Fact]
    public async Task Stop_source_exception_still_invalidates_session_reports_stop_and_releases_reservation()
    {
        await _Start.Handle(new(), _StartOutput, CancellationToken.None);
        TrainingSession session = _Store.LoadActive().Value;
        IStopTrainingOutputPort output = Substitute.For<IStopTrainingOutputPort>();
        _Frames.Stop(Arg.Any<CancellationToken>()).Returns<Task>(_ => throw new InvalidOperationException("source"));
        StopTrainingInteractor stop = new(_Store, _Frames, _SessionGate);
        await Assert.ThrowsAsync<InvalidOperationException>(() => stop.Handle(new(), output, CancellationToken.None));
        Assert.False(_Store.HasActive);
        Assert.Equal(TrainingState.Stopped, session.State);
        await output.Received(1).ReportStopped(Arg.Is<StopTrainingResponse>(r => r.SessionId == session.Id));
        using SessionGate.Reservation? next = _SessionGate.TryReserve();
        Assert.NotNull(next);
    }

    [Fact]
    public async Task Start_exception_or_cancellation_cleans_up_before_propagating()
    {
        _Frames.Start(Arg.Any<CancellationToken>()).Returns<Task<FrameSourceResult>>(_ => throw new OperationCanceledException());
        await Assert.ThrowsAsync<OperationCanceledException>(() => _Start.Handle(new(), _StartOutput, CancellationToken.None));
        Assert.False(_Store.HasActive);
        await _Frames.Received(1).Stop(CancellationToken.None);
    }

    [Fact]
    public async Task Stop_ends_session_and_restart_has_fresh_tracking_and_attempts()
    {
        await _Start.Handle(new(), _StartOutput, TestContext.Current.CancellationToken);
        TrainingSession first = _Store.LoadActive().Value;
        first.ApplyTracking(Snapshot(0, SnakePosition(Team.A)));
        first.ApplyTracking(Snapshot(1000, SnakePosition(Team.A)));
        IStopTrainingOutputPort output = Substitute.For<IStopTrainingOutputPort>();
        StopTrainingInteractor stop = new(_Store, _Frames, _SessionGate);
        await stop.Handle(new(), output, TestContext.Current.CancellationToken);
        Assert.False(_Store.HasActive);
        Assert.Equal(TrainingState.Stopped, first.State);
        await output.Received(1).ReportStopped(Arg.Is<StopTrainingResponse>(response =>
            response.SessionId == first.Id));
        await stop.Handle(new(), output, TestContext.Current.CancellationToken);
        await output.Received(1).ReportStopFailed(Arg.Any<string>());

        await _Start.Handle(new(), _StartOutput, TestContext.Current.CancellationToken);
        TrainingSession second = _Store.LoadActive().Value;
        Assert.NotEqual(first.Id, second.Id);
        Assert.Null(second.Attempt);
        TrainingFrameResult lost = second.ApplyObservations(At(2000), []);
        Assert.False(lost.Tracking.IsBallFound);
        Assert.Equal(BallPossession.None, lost.Tracking.Possession);
        Assert.Empty(lost.Events);
        second.ApplyTracking(Snapshot(3000, SnakePosition(Team.A)));
        Assert.Equal(1, Assert.Single(second.ApplyTracking(Snapshot(4000, SnakePosition(Team.A))).Events.OfType<TrainingPreparationConfirmed>()).Attempt.AttemptId);
    }

    [Fact]
    public async Task Skips_when_no_active_training_session()
    {
        IFrameVisionOps vision = Substitute.For<IFrameVisionOps>();
        IProcessFrameOutputPort output = Substitute.For<IProcessFrameOutputPort>();
        ProcessFrameInteractor process = new(_Store);
        await process.Handle(new(At(0), vision), output, TestContext.Current.CancellationToken);
        vision.DidNotReceive().DetectBalls(Arg.Any<TableConfiguration>());
        await output.Received(1).ReportSkipped(Arg.Any<string>());
    }

    [Theory]
    [InlineData(TrainingVariantAnnouncement.OnReady, TrackingStatus.Observed)]
    [InlineData(TrainingVariantAnnouncement.OnShoot, TrackingStatus.Predicted)]
    public async Task Complete_first_attempt_is_testable_through_frame_usecase_with_no_camera_network_or_audio(
        TrainingVariantAnnouncement mode, TrackingStatus trackingStatus)
    {
        IBallTracker balls = Substitute.For<IBallTracker>();
        balls.ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>()).Returns(call =>
        {
            Frame frame = call.Arg<Frame>();
            long ms = frame.TimestampNs / 1_000_000;
            Point position = ms <= 2000 ? SnakePosition(Team.A) : ms == 2100 ? new(1200, 450) : new(1300, 450);
            Vector2 velocity = ms == 2100 ? new(2000, 0) : default;
            return new TrackingSnapshot(frame, [new(1, frame, position, TrackingConfidence.High, trackingStatus, velocity)]);
        });
        LiveTracker tracker = new(LiveTrackerParams.Default, balls, new PossessionCalculator(TableConfig.Config));
        TrainingSession session = new(Guid.NewGuid(), tracker, TableConfig.Config, new([Option(TrainingStrategyKind.SnakeShot)], mode), new CountingRandom());
        _Store.SaveActive(session);
        IFrameVisionOps vision = Substitute.For<IFrameVisionOps>();
        ObservedBall observation = new(SnakePosition(Team.A), ObservationQualityThresholds.Default.HighQuality);
        ObservedBall[] observations = trackingStatus == TrackingStatus.Observed ? [observation] : [];
        vision.DetectBalls(TableConfig.Config).Returns(observations);
        List<ProcessFrameResponse> responses = [];
        IProcessFrameOutputPort output = Substitute.For<IProcessFrameOutputPort>();
        output.ReportProcessed(Arg.Do<ProcessFrameResponse>(responses.Add)).Returns(Task.CompletedTask);
        ProcessFrameInteractor process = new(_Store);

        foreach (long ms in new long[] { 0, 1000, 2000, 2100, 2200, 2450, 2500 })
        {
            await process.Handle(new(At(ms), vision), output, TestContext.Current.CancellationToken);
        }

        Assert.Equal(7, responses.Count);
        Assert.All(responses, response => Assert.Equal(session.Id, response.SessionId));
        Assert.All(responses, response => Assert.Equal(observations, response.Tracking.Observations));
        Assert.Single(responses, response => response.PreparationConfirmed is not null);
        Assert.Equal(2, responses.Count(response => response.Announcement is not null));
        ProcessFrameResponse ready = responses[1];
        Assert.NotNull(ready.PreparationConfirmed);
        TrainingAnnouncementRequest readyAnnouncement = Assert.IsType<TrainingAnnouncementRequest>(ready.Announcement);
        Assert.Equal(TrainingAnnouncementKind.Ready, readyAnnouncement.Kind);
        Assert.Equal(mode == TrainingVariantAnnouncement.OnReady ? "Left ready" : "Snake ready", readyAnnouncement.Text);
        Assert.Equal(1, readyAnnouncement.AnnouncementId);
        Assert.Equal(mode == TrainingVariantAnnouncement.OnReady ? new TrainingVariant("left", "Left", 1) : null, readyAnnouncement.Variant);
        Assert.Equal(readyAnnouncement.Variant, ready.Status.Variant);
        Assert.Equal(TrainingState.Waiting, ready.Status.State);
        Assert.Equal(1, ready.Status.Attempt!.AttemptId);
        Assert.Equal(ready.Status.Attempt, ready.PreparationConfirmed);
        Assert.Equal(ready.PreparationConfirmed, readyAnnouncement.Attempt);
        ProcessFrameResponse variant = responses[2];
        TrainingAnnouncementRequest variantAnnouncement = Assert.IsType<TrainingAnnouncementRequest>(variant.Announcement);
        Assert.Equal(mode == TrainingVariantAnnouncement.OnReady ? TrainingAnnouncementKind.Shoot : TrainingAnnouncementKind.Variant, variantAnnouncement.Kind);
        Assert.Equal(mode == TrainingVariantAnnouncement.OnReady ? "Shoot!" : "Left", variantAnnouncement.Text);
        Assert.Equal(2, variantAnnouncement.AnnouncementId);
        Assert.Equal(new TrainingVariant("left", "Left", 1), variantAnnouncement.Variant);
        Assert.Equal(variantAnnouncement.Variant, variant.Status.Variant);
        Assert.Equal(ready.Status.Attempt, variant.Status.Attempt);
        ProcessFrameResponse completed = Assert.Single(responses, response => response.CompletedAttempt is not null);
        TrainingAttemptCompletion completion = Assert.IsType<TrainingAttemptCompletion>(completed.CompletedAttempt);
        Assert.Equal(At(1000), completion.Attempt.Anchor.Frame);
        Assert.Equal(At(2200), completion.ConfirmationFrame);
        Assert.Equal(At(2450), completion.CompletionFrame);
        Assert.Equal(variant.Status.Variant, completion.Variant);
        Assert.Equal(completed.Status.Attempt, completion.Attempt);
        Assert.Equal(TrainingState.Completed, completed.Status.State);
        Assert.Null(completed.Announcement);
        Assert.Null(completed.AbortedAttempt);
        Assert.Single(responses, response => response.Replay is not null);
        Assert.NotNull(completed.Replay);
        Assert.Equal(At(1000), completed.Replay.AnchorFrame);
        Assert.Equal(700_000_000, completed.Replay.StartTimestampNs);
        Assert.Equal(2_500_000_000, completed.Replay.EndTimestampNs);
        Assert.Equal(0.25, completed.Replay.Speed);
        Assert.Equal(1, completed.Replay.RequiredCompletedLoops);
        Assert.Null(responses.Last().CompletedAttempt);
        Assert.Null(responses.Last().Announcement);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Frame_response_groups_tracking_calibration_and_training_status(bool scene)
    {
        IBallTracker balls = Substitute.For<IBallTracker>();
        Point position = SnakePosition(Team.A);
        Vector2 velocity = new(20, 30);
        balls.ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>()).Returns(call =>
        {
            Frame frame = call.Arg<Frame>();
            return new TrackingSnapshot(frame, [new(1, frame, position, TrackingConfidence.High, TrackingStatus.Observed, velocity)]);
        });
        LiveTracker tracker = new(LiveTrackerParams.Default, balls, new PossessionCalculator(TableConfig.Config));
        TrainingSession session = new(Guid.NewGuid(), tracker, TableConfig.Config, TrainingOptions.Default, new CountingRandom());
        _Store.SaveActive(session);
        IFrameVisionOps vision = Substitute.For<IFrameVisionOps>();
        ObservedBall observation = new(position, ObservationQualityThresholds.Default.HighQuality);
        vision.DetectBalls(TableConfig.Config).Returns([observation]);
        IProcessFrameOutputPort output = Substitute.For<IProcessFrameOutputPort>();
        ProcessFrameResponse? response = null;
        output.ReportProcessed(Arg.Do<ProcessFrameResponse>(value => response = value)).Returns(Task.CompletedTask);
        Frame frame = At(scene ? 1000 : 10000);

        await new ProcessFrameInteractor(_Store).Handle(new(frame, vision), output, TestContext.Current.CancellationToken);

        Assert.NotNull(response);
        Assert.Equal(session.Id, response.SessionId);
        TrackingFrameResponse tracking = response.Tracking;
        Assert.Equal(frame, tracking.Frame);
        Assert.True(tracking.IsBallFound);
        Assert.Equal(position, tracking.BallPosition);
        Assert.Equal(velocity, tracking.BallVelocityPxPerS);
        Assert.Equal(position, Assert.Single(tracking.BallCandidates).Position);
        Assert.Equal(observation, Assert.Single(tracking.Observations));
        Assert.Equal(new BallPossession(Team.A, PossessionArea.ThreeBar), tracking.Possession);
        Assert.Equal(tracking.Possession, tracking.LastKnownPossession);
        Assert.Equal(0, tracking.PossessionTimeMs);
        Assert.Equal(LiveTrackingPhase.Live, tracking.TrackingPhase);
        Assert.Equal(0, tracking.LastObservedBallAgeMs);
        Assert.Equal(BarType.A3, tracking.ClosestBar);
        Assert.Equal(!scene, tracking.RequestTableConfigUpdate);
        Assert.Equal(scene, tracking.RequestTableSceneUpdate);
        Assert.Equal(TrainingState.Searching, response.Status.State);
        Assert.Null(response.Status.Attempt);
        Assert.Null(response.Status.Variant);
        Assert.Null(response.PreparationConfirmed);
        Assert.Null(response.AnnouncementInvalidation);
        Assert.Null(response.Announcement);
        Assert.Null(response.AbortedAttempt);
        Assert.Null(response.CompletedAttempt);
        Assert.Null(response.Replay);
    }

    [Fact]
    public async Task Abort_response_preserves_correlation_and_next_ready_invalidates_previous_abort()
    {
        IBallTracker balls = Substitute.For<IBallTracker>();
        balls.ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>()).Returns(call =>
        {
            Frame frame = call.Arg<Frame>();
            long ms = frame.TimestampNs / 1_000_000;
            Point position = ms >= 2100 && ms <= 3000 ? new(1180, 450) : SnakePosition(Team.A);
            return new TrackingSnapshot(frame, [new(1, frame, position, TrackingConfidence.High, TrackingStatus.Observed, default)]);
        });
        LiveTracker tracker = new(LiveTrackerParams.Default, balls, new PossessionCalculator(TableConfig.Config));
        TrainingSession session = new(Guid.NewGuid(), tracker, TableConfig.Config, new([Option(TrainingStrategyKind.SnakeShot)], TrainingVariantAnnouncement.OnShoot), new CountingRandom());
        _Store.SaveActive(session);
        IFrameVisionOps vision = Substitute.For<IFrameVisionOps>();
        vision.DetectBalls(TableConfig.Config).Returns([]);
        List<ProcessFrameResponse> responses = [];
        IProcessFrameOutputPort output = Substitute.For<IProcessFrameOutputPort>();
        output.ReportProcessed(Arg.Do<ProcessFrameResponse>(responses.Add)).Returns(Task.CompletedTask);
        ProcessFrameInteractor process = new(_Store);

        foreach (long ms in new long[] { 0, 1000, 2000, 2100, 2850, 3000, 3100, 4100 })
        {
            await process.Handle(new(At(ms), vision), output, TestContext.Current.CancellationToken);
        }

        ProcessFrameResponse aborted = responses[4];
        Assert.Equal(TrainingState.Aborted, aborted.Status.State);
        TrainingAnnouncementInvalidation invalidation = Assert.IsType<TrainingAnnouncementInvalidation>(aborted.AnnouncementInvalidation);
        TrainingAttemptAbort abort = Assert.IsType<TrainingAttemptAbort>(aborted.AbortedAttempt);
        TrainingAnnouncementRequest announcement = Assert.IsType<TrainingAnnouncementRequest>(aborted.Announcement);
        Assert.Equal(TrainingAbortReason.PreparationLost, abort.Reason);
        Assert.Equal(TrainingAnnouncementKind.Aborted, announcement.Kind);
        Assert.Equal("Aborted", announcement.Text);
        Assert.Null(announcement.Variant);
        Assert.NotNull(aborted.Status.Variant);
        Assert.Equal(2, invalidation.ThroughAnnouncementId);
        Assert.Equal(3, announcement.AnnouncementId);
        Assert.Equal(aborted.Status.Attempt, invalidation.Attempt);
        Assert.Equal(abort.Attempt, invalidation.Attempt);
        Assert.Equal(abort.Attempt, announcement.Attempt);
        Assert.Null(aborted.PreparationConfirmed);
        Assert.Null(aborted.CompletedAttempt);
        Assert.All(responses, response => Assert.Null(response.Replay));
        Assert.Null(responses[5].AbortedAttempt);
        Assert.Null(responses[5].Announcement);
        Assert.Null(responses[5].AnnouncementInvalidation);
        ProcessFrameResponse nextReady = responses.Last();
        TrainingAnnouncementInvalidation nextInvalidation = Assert.IsType<TrainingAnnouncementInvalidation>(nextReady.AnnouncementInvalidation);
        TrainingAnnouncementRequest nextAnnouncement = Assert.IsType<TrainingAnnouncementRequest>(nextReady.Announcement);
        Assert.Equal(announcement.AnnouncementId, nextInvalidation.ThroughAnnouncementId);
        Assert.Equal(aborted.Status.Attempt, nextInvalidation.Attempt);
        Assert.NotNull(nextReady.PreparationConfirmed);
        Assert.Equal(2, nextReady.PreparationConfirmed.AttemptId);
        Assert.Equal(nextReady.Status.Attempt, nextReady.PreparationConfirmed);
        Assert.Equal(nextReady.PreparationConfirmed, nextAnnouncement.Attempt);
        Assert.Equal(TrainingAnnouncementKind.Ready, nextAnnouncement.Kind);
        Assert.Equal(4, nextAnnouncement.AnnouncementId);
    }

    [Fact]
    public async Task Stop_before_preparation_reports_the_stopped_session()
    {
        await _Start.Handle(new(), _StartOutput, TestContext.Current.CancellationToken);
        Guid sessionId = _Store.LoadActive().Value.Id;
        IStopTrainingOutputPort output = Substitute.For<IStopTrainingOutputPort>();

        await new StopTrainingInteractor(_Store, _Frames, _SessionGate).Handle(new(), output, TestContext.Current.CancellationToken);

        await output.Received(1).ReportStopped(Arg.Is<StopTrainingResponse>(response =>
            response.SessionId == sessionId));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Calibration_completion_only_unlocks_matching_session(bool scene, bool stale)
    {
        TrainingSession session = Session();
        _Store.SaveActive(session);
        long ms = scene ? 1000 : 10000;
        TrainingFrameResult initial = session.ApplyTracking(Snapshot(ms, SnakePosition(Team.A)));
        Assert.Equal(!scene, initial.RequestTableConfigUpdate);
        Assert.Equal(scene, initial.RequestTableSceneUpdate);
        Guid id = stale ? Guid.NewGuid() : session.Id;
        if (scene)
        {
            await new CompleteTableSceneUpdateInteractor(_Store).Handle(new(id), TestContext.Current.CancellationToken);
        }
        else
        {
            await new CompleteTableUpdateInteractor(_Store).Handle(new(id), TestContext.Current.CancellationToken);
        }

        TrainingFrameResult next = session.ApplyTracking(Snapshot(ms + 10000, SnakePosition(Team.A)));
        Assert.Equal(!stale, next.RequestTableConfigUpdate);
        _Store.Clear();
        await new CompleteTableUpdateInteractor(_Store).Handle(new(session.Id), TestContext.Current.CancellationToken);
        await new CompleteTableSceneUpdateInteractor(_Store).Handle(new(session.Id), TestContext.Current.CancellationToken);
        Assert.False(_Store.HasActive);
    }
}
