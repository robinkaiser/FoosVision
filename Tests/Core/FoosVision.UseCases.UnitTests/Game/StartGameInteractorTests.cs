// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Entities;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.UnitTests;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.StartGame;
using FoosVision.UseCases.Game.StopGame;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Game;

public class StartGameInteractorTests
{
    private readonly FakeGameSessionStore _FakeStore;
    private readonly IStartGameOutputPort _Output;
    private readonly ISettingsStore _Settings;
    private readonly IFrameSource _FrameSource;

    private readonly StartGameInteractor _Testee;

    public StartGameInteractorTests()
    {
        _FakeStore = new();
        _Output = Substitute.For<IStartGameOutputPort>();
        _Settings = Substitute.For<ISettingsStore>();
        _FrameSource = Substitute.For<IFrameSource>();

        _Testee = new StartGameInteractor(_FakeStore, _Settings, _FrameSource);

        _Settings.LoadTableConfig().Returns(Option<TableConfiguration>.Some(TableConfig.Config));
        _FrameSource.Configure(Arg.Any<CancellationToken>()).Returns(FrameSourceResult.Success);
    }

    [Fact]
    public async Task Start_fails_if_a_game_session_is_already_active()
    {
        await _Testee.Handle(new(), _Output, CancellationToken.None);

        await _Output.DidNotReceiveWithAnyArgs().ReportStarted(default!);
        await _Output.Received().ReportStartFailed(Arg.Any<string>());
    }

    [Fact]
    public async Task Start_fails_when_no_table_config_is_available()
    {
        _Settings.LoadTableConfig().Returns(Option<TableConfiguration>.None());

        await _Testee.Handle(new(), _Output, CancellationToken.None);

        await _Output.DidNotReceiveWithAnyArgs().ReportStarted(default!);
        await _Output.Received().ReportStartFailed(Arg.Any<string>());
    }

    [Fact]
    public async Task Start_fails_when_if_frame_source_configuration_fails()
    {
        _FrameSource.Configure(Arg.Any<CancellationToken>()).Returns(FrameSourceResult.Failure);

        await _Testee.Handle(new(), _Output, CancellationToken.None);

        await _Output.DidNotReceiveWithAnyArgs().ReportStarted(default!);
        await _Output.Received().ReportStartFailed(Arg.Any<string>());
    }

    [Fact]
    public async Task Start_succeeds()
    {
        _FakeStore.Clear();

        await _Testee.Handle(new(), _Output, CancellationToken.None);

        await _Output.DidNotReceiveWithAnyArgs().ReportStartFailed(Arg.Any<string>());

        Assert.True(_FakeStore.LoadActive().TryGetValue(out var session));
        await _Output.Received().ReportStarted(Arg.Is<StartGameResponse>(r => r.SessionId == session.Id));
    }

    [Fact]
    public async Task Restart_creates_fresh_ball_possession_and_replay_state()
    {
        _FakeStore.Clear();
        await _Testee.Handle(new(), _Output, CancellationToken.None);
        Assert.True(_FakeStore.LoadActive().TryGetValue(out GameSession first));
        ObservedBall observation = new(new Point(1100, 450), ObservationQualityThresholds.Default.HighQuality);
        first.ApplyObservations(new Frame(1, 1_000_000_000L), [observation]);
        first.ApplyObservations(new Frame(2, 1_500_000_000L), [observation]);
        first.ApplyObservations(new Frame(3, 2_000_000_000L), [observation]);

        StopGameInteractor stop = new(_FakeStore, _FrameSource);
        await stop.Handle(new(), Substitute.For<IStopGameOutputPort>(), CancellationToken.None);
        await _Testee.Handle(new(), _Output, CancellationToken.None);
        Assert.True(_FakeStore.LoadActive().TryGetValue(out GameSession second));
        Assert.NotEqual(first.Id, second.Id);

        Frame lostFrame = new(4, 3_000_000_000L);
        IReadOnlyList<Change> changes = second.ApplyObservations(lostFrame, []);
        TrackedBallInfo lost = Assert.Single(changes.OfType<TrackedBallInfo>());
        Assert.False(lost.IsFound);
        Assert.Empty(lost.Candidates);
        Assert.Equal(BallPossession.None, lost.Possession);
        Assert.Equal(0, lost.PossessionTimeMs);
        Assert.DoesNotContain(changes, change => change is ReplayRequest);

        IReadOnlyList<Change> recovered = second.ApplyObservations(new Frame(5, 3_100_000_000L), [observation]);
        TrackedBallInfo found = Assert.Single(recovered.OfType<TrackedBallInfo>());
        Assert.True(found.IsFound);
        Assert.Equal(0, found.PossessionTimeMs);
        Assert.Empty(found.Candidates);

        // The first session had enough history for a replay; it must not arm the second one.
        Assert.Single(first.ApplyObservations(lostFrame, []).OfType<ReplayRequest>());
    }
}
