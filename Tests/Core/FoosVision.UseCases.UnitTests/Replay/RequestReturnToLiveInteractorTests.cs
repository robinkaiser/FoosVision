// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.CompleteReplayLoop;
using FoosVision.UseCases.Replay.RequestReturnToLive;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Replay;

public class RequestReturnToLiveInteractorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Returns_only_after_recorder_request_and_completed_loop_in_either_order(bool requestFirst)
    {
        FakeReplaySessionStore store = new();
        ReplayId replayId = new(42, 1_000_000);
        ReplaySession session = ReplaySessionTestFactory.CreateStarted(replayId);
        store.SaveActive(session);
        RecordingOutput output = new();
        RequestReturnToLiveInteractor request = new(store);
        CompleteReplayLoopInteractor loop = new(store);
        if (requestFirst)
        {
            await request.Handle(new(replayId), output, CancellationToken.None);
            Assert.True(store.HasActive);
            Assert.Empty(output.Returns);
            await loop.Handle(new(), output, CancellationToken.None);
        }
        else
        {
            await loop.Handle(new(), output, CancellationToken.None);
            Assert.True(store.HasActive);
            Assert.Empty(output.Returns);
            await request.Handle(new(replayId), output, CancellationToken.None);
        }

        Assert.False(store.HasActive);
        Assert.Equal(new(replayId), Assert.Single(output.Returns));
        await request.Handle(new(replayId), output, CancellationToken.None);
        Assert.Single(output.Returns);
    }

    [Fact]
    public async Task Old_request_does_not_end_current_replay()
    {
        FakeReplaySessionStore store = new();
        ReplaySession session = ReplaySessionTestFactory.CreateStarted(new(84, 2_000_000));
        session.CompleteLoop();
        store.SaveActive(session);
        IRequestReturnToLiveOutputPort output = Substitute.For<IRequestReturnToLiveOutputPort>();
        RequestReturnToLiveInteractor sut = new(store);
        await sut.Handle(new(new(42, 1_000_000)), output, CancellationToken.None);
        Assert.True(store.HasActive);
        await output.DidNotReceive().ReportReturnToLive(Arg.Any<ReturnToLiveResponse>());
    }

    private sealed class RecordingOutput : ICompleteReplayLoopOutputPort, IRequestReturnToLiveOutputPort
    {
        public List<ReturnToLiveResponse> Returns { get; } = [];

        public Task ReportReturnToLive(ReturnToLiveResponse response)
        {
            Returns.Add(response);
            return Task.CompletedTask;
        }

        public Task ReportSkipped(string reason) => Task.CompletedTask;
    }
}
