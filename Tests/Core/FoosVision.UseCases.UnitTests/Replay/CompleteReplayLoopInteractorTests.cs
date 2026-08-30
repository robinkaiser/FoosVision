// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.CompleteReplayLoop;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Replay;

public class CompleteReplayLoopInteractorTests
{
    private readonly FakeReplaySessionStore _FakeStore = new();
    private readonly ICompleteReplayLoopOutputPort _Output = Substitute.For<ICompleteReplayLoopOutputPort>();
    private readonly CompleteReplayLoopInteractor _Testee;

    public CompleteReplayLoopInteractorTests()
    {
        _Testee = new CompleteReplayLoopInteractor(_FakeStore);
    }

    [Fact]
    public async Task Complete_loop_updates_active_replay()
    {
        ReplaySession session = ReplaySessionTestFactory.CreateStarted(new ReplayId(42, 1_000_000));
        _FakeStore.SaveActive(session);

        await _Testee.Handle(new CompleteReplayLoopRequest(), _Output, CancellationToken.None);

        Assert.Equal(1, _FakeStore.LoadActive().Value.CompletedLoops);
    }

    [Fact]
    public async Task Complete_loop_skips_without_active_replay()
    {
        await _Testee.Handle(new CompleteReplayLoopRequest(), _Output, CancellationToken.None);

        await _Output.Received().ReportSkipped("No active replay.");
    }
}
