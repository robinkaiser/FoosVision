// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.StopReplay;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Replay;

public class StopReplayInteractorTests
{
    private readonly FakeReplaySessionStore _FakeStore = new();
    private readonly IStopReplayOutputPort _Output;
    private readonly List<ReplayStoppedResponse> _Stopped = [];
    private readonly StopReplayInteractor _Testee;

    public StopReplayInteractorTests()
    {
        _Output = Substitute.For<IStopReplayOutputPort>();
        _Output.ReportStopped(Arg.Any<ReplayStoppedResponse>())
            .Returns(ci =>
            {
                _Stopped.Add(ci.Arg<ReplayStoppedResponse>());
                return Task.CompletedTask;
            });

        _Testee = new StopReplayInteractor(_FakeStore);
    }

    [Fact]
    public async Task Stop_reports_active_replay_stopped()
    {
        ReplayId replayId = new(42, 1_000_000);
        ReplaySession session = ReplaySessionTestFactory.CreateStarted(replayId);
        _FakeStore.SaveActive(session);

        await _Testee.Handle(new StopReplayRequest(), _Output, CancellationToken.None);

        ReplayStoppedResponse response = Assert.Single(_Stopped);
        Assert.Equal(replayId, response.ReplayId);
        Assert.False(_FakeStore.HasActive);
    }

    [Fact]
    public async Task Stop_skips_without_active_replay()
    {
        await _Testee.Handle(new StopReplayRequest(), _Output, CancellationToken.None);

        await _Output.Received().ReportStopFailed("No active replay.");
    }
}
