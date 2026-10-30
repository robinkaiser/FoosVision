// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Entities;
using FoosVision.Domain.Game.Services.GameTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.UseCases.Game.CompleteTableUpdate;
using FoosVision.UseCases.Game.CompleteTableSceneUpdate;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Game;

public class CompleteCalibrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Only_matching_session_can_complete_calibration(bool scene, bool stale)
    {
        FakeGameSessionStore store = new();
        GameSession session = store.LoadActive().Value;
        Frame frame = new(1, scene ? 1_000_000_000 : 10_000_000_000);
        TrackedBall ball = new(1, frame, new Point(100, 100), TrackingConfidence.High, TrackingStatus.Observed, default);
        GameTrackingSnapshot snapshot = new(frame, GameTrackingPhase.Live, true, ball.Position,
            default, [ball], BallPossession.None, BallPossession.None, 0, 0, false, false, null);
        store.GameTracker.ApplyObservations(Arg.Any<Frame>(), Arg.Any<IEnumerable<ObservedBall>>()).Returns(snapshot);
        session.ApplyObservations(frame, []);
        Guid requestSession = stale ? Guid.NewGuid() : session.Id;

        if (scene)
        {
            CompleteTableSceneUpdateInteractor interactor = new(store);
            await interactor.Handle(new(requestSession), TestContext.Current.CancellationToken);
        }
        else
        {
            CompleteTableUpdateInteractor interactor = new(store);
            await interactor.Handle(new(requestSession), TestContext.Current.CancellationToken);
        }

        Frame next = new(2, frame.TimestampNs + 10_000_000_000);
        IReadOnlyList<Change> changes = session.ApplyObservations(next, []);
        Assert.Equal(!stale, changes.Any(c => c is UpdateTableConfigRequest));
    }

    [Fact]
    public async Task Completion_after_stop_is_ignored()
    {
        FakeGameSessionStore store = new();
        Guid id = store.LoadActive().Value.Id;
        store.Clear();
        await new CompleteTableUpdateInteractor(store).Handle(new(id), TestContext.Current.CancellationToken);
        await new CompleteTableSceneUpdateInteractor(store).Handle(new(id), TestContext.Current.CancellationToken);
        Assert.False(store.HasActive);
    }
}
