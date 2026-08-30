// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.ObserveLiveTracking;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Replay;

public class ObserveLiveTrackingInteractorTests
{
    private readonly FakeReplaySessionStore _FakeStore = new();
    private readonly IObserveLiveTrackingOutputPort _Output;
    private readonly List<ReturnToLiveResponse> _ReturnToLive = [];
    private readonly ObserveLiveTrackingInteractor _Testee;

    public ObserveLiveTrackingInteractorTests()
    {
        _Output = Substitute.For<IObserveLiveTrackingOutputPort>();
        _Output.ReportReturnToLive(Arg.Any<ReturnToLiveResponse>())
            .Returns(ci =>
            {
                _ReturnToLive.Add(ci.Arg<ReturnToLiveResponse>());
                return Task.CompletedTask;
            });

        _Testee = new ObserveLiveTrackingInteractor(_FakeStore);
    }

    [Fact]
    public async Task Does_not_return_to_live_before_required_loops()
    {
        SaveReplayWithCompletedLoops(1, requiredCompletedLoops: 2);

        await _Testee.Handle(new ObserveLiveTrackingRequest(new Point(100, 200)), _Output, CancellationToken.None);

        Assert.Empty(_ReturnToLive);
        Assert.True(_FakeStore.HasActive);
    }

    [Fact]
    public async Task Does_not_return_to_live_after_required_loops_without_live_ball()
    {
        SaveReplayWithCompletedLoops(1);

        await _Testee.Handle(new ObserveLiveTrackingRequest(null), _Output, CancellationToken.None);

        Assert.Empty(_ReturnToLive);
        Assert.True(_FakeStore.HasActive);
    }

    [Fact]
    public async Task Does_not_return_to_live_when_first_live_ball_after_required_loops_is_found()
    {
        SaveReplayWithCompletedLoops(1);

        await _Testee.Handle(new ObserveLiveTrackingRequest(new Point(100, 200)), _Output, CancellationToken.None);

        Assert.Empty(_ReturnToLive);
        Assert.True(_FakeStore.HasActive);
    }

    [Fact]
    public async Task Does_not_return_to_live_when_live_ball_moves_less_than_ten_pixels()
    {
        SaveReplayWithCompletedLoops(1);

        await _Testee.Handle(new ObserveLiveTrackingRequest(new Point(100, 200)), _Output, CancellationToken.None);
        await _Testee.Handle(new ObserveLiveTrackingRequest(new Point(109, 200)), _Output, CancellationToken.None);

        Assert.Empty(_ReturnToLive);
        Assert.True(_FakeStore.HasActive);
    }

    [Fact]
    public async Task Returns_to_live_when_live_ball_moves_ten_pixels()
    {
        ReplayId replayId = SaveReplayWithCompletedLoops(1);

        await _Testee.Handle(new ObserveLiveTrackingRequest(new Point(100, 200)), _Output, CancellationToken.None);
        await _Testee.Handle(new ObserveLiveTrackingRequest(new Point(110, 200)), _Output, CancellationToken.None);

        ReturnToLiveResponse response = Assert.Single(_ReturnToLive);
        Assert.Equal(replayId, response.ReplayId);
        Assert.False(_FakeStore.HasActive);
    }

    private ReplayId SaveReplayWithCompletedLoops(
        int completedLoops,
        int requiredCompletedLoops = ReplaySession.DefaultRequiredCompletedLoops)
    {
        ReplayId replayId = new(42, 1_000_000);
        ReplaySession session = ReplaySessionTestFactory.CreateStarted(replayId, requiredCompletedLoops);

        for (int i = 0; i < completedLoops; i++)
        {
            session.CompleteLoop();
        }

        _FakeStore.SaveActive(session);
        return replayId;
    }
}
