// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.UseCases.Dependencies.Video;
using FoosVision.UseCases.Game.StopGame;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Game;

public class StopGameInteractorTests
{
    private readonly FakeGameSessionStore _FakeStore;
    private readonly IStopGameOutputPort _Output;
    private readonly IFrameSource _FrameSource;

    private readonly StopGameInteractor _Testee;

    public StopGameInteractorTests()
    {
        _FakeStore = new();
        _Output = Substitute.For<IStopGameOutputPort>();
        _FrameSource = Substitute.For<IFrameSource>();

        _Testee = new StopGameInteractor(_FakeStore, _FrameSource);
    }

    [Fact]
    public async Task Stop_fails_when_no_game_session_is_active()
    {
        _FakeStore.Clear();

        await _Testee.Handle(new(), _Output, CancellationToken.None);

        await _Output.DidNotReceiveWithAnyArgs().ReportStopped(default!);
        await _Output.Received().ReportStopFailed(Arg.Any<string>());
    }

    [Fact]
    public async Task Stop_succeeds()
    {
        _FakeStore.LoadActive().TryGetValue(out var session);
        var id = session.Id;

        await _Testee.Handle(new(), _Output, CancellationToken.None);

        await _Output.DidNotReceiveWithAnyArgs().ReportStopFailed(Arg.Any<string>());
        await _Output.Received().ReportStopped(Arg.Is<StopGameResponse>(r =>
            r.SessionId == id));
    }
}
