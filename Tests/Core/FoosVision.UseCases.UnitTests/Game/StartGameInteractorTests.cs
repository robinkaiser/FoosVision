// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.UnitTests;
using FoosVision.UseCases.Dependencies.Settings;
using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.StartGame;
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
}
