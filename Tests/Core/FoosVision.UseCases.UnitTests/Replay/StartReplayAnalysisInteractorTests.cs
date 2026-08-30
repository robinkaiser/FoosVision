// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.UnitTests;
using FoosVision.UseCases.Replay.StartReplayAnalysis;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Replay;

public class StartReplayAnalysisInteractorTests
{
    private readonly FakeReplaySessionStore _FakeStore = new();
    private readonly IStartReplayAnalysisOutputPort _Output;
    private readonly List<ReplayAnalysisStartedResponse> _Started = [];
    private readonly StartReplayAnalysisInteractor _Testee;

    public StartReplayAnalysisInteractorTests()
    {
        _Output = Substitute.For<IStartReplayAnalysisOutputPort>();
        _Output.ReportReplayAnalysisStarted(Arg.Any<ReplayAnalysisStartedResponse>())
            .Returns(ci =>
            {
                _Started.Add(ci.Arg<ReplayAnalysisStartedResponse>());
                return Task.CompletedTask;
            });
        _Testee = new StartReplayAnalysisInteractor(_FakeStore);
    }

    [Fact]
    public async Task Start_analysis_stores_session_and_reports_started()
    {
        ReplayId replayId = new(42, 1_000_000);

        await _Testee.Handle(CreateRequest(replayId, new Point(100, 200)), _Output, CancellationToken.None);

        Assert.True(_FakeStore.HasActive);
        ReplaySession session = _FakeStore.LoadActive().Value;
        Assert.Equal(replayId, session.CurrentReplayId.Value);
        Assert.True(session.TableConfiguration.HasValue);

        ReplayAnalysisStartedResponse started = Assert.Single(_Started);
        Assert.Equal(replayId, started.ReplayId);
    }

    [Fact]
    public async Task Start_analysis_replaces_active_replay()
    {
        ReplayId firstReplay = new(42, 1_000_000);
        await _Testee.Handle(CreateRequest(firstReplay, new Point(100, 200)), _Output, CancellationToken.None);
        _FakeStore.LoadActive().Value.CompleteLoop();

        ReplayId replacementReplay = new(84, 2_000_000);

        await _Testee.Handle(CreateRequest(replacementReplay, new Point(300, 400)), _Output, CancellationToken.None);

        ReplaySession session = _FakeStore.LoadActive().Value;
        Assert.Equal(replacementReplay, session.CurrentReplayId.Value);
        Assert.Equal(0, session.CompletedLoops);
    }

    private static StartReplayAnalysisRequest CreateRequest(ReplayId replayId, Point lastKnownBallPosition)
        => new(
            replayId,
            new ReplayTrackAnchor(new Frame(40, 1_000_000_000), lastKnownBallPosition),
            TableConfig.Config);
}
