// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.UseCases.Replay.CompleteReplayAnalysis;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Replay;

public class CompleteReplayAnalysisInteractorTests
{
    private readonly FakeReplaySessionStore _FakeStore = new();
    private readonly ICompleteReplayAnalysisOutputPort _Output;
    private readonly List<ReplayAnalysisCompletedResponse> _Completed = [];
    private readonly CompleteReplayAnalysisInteractor _Testee;

    public CompleteReplayAnalysisInteractorTests()
    {
        _Output = Substitute.For<ICompleteReplayAnalysisOutputPort>();
        _Output.ReportReplayAnalysisCompleted(Arg.Any<ReplayAnalysisCompletedResponse>())
            .Returns(ci =>
            {
                _Completed.Add(ci.Arg<ReplayAnalysisCompletedResponse>());
                return Task.CompletedTask;
            });
        _Testee = new CompleteReplayAnalysisInteractor(_FakeStore);
    }

    [Fact]
    public async Task Complete_analysis_reports_domain_analysis()
    {
        ReplayId replayId = new(42, 1_000_000);
        _FakeStore.SaveActive(ReplaySessionTestFactory.CreateStarted(replayId));

        await _Testee.Handle(new CompleteReplayAnalysisRequest(), _Output, CancellationToken.None);

        ReplayAnalysisCompletedResponse completed = Assert.Single(_Completed);
        Assert.Equal(replayId, completed.ReplayId);
        ReplayAnalysisFrame frame = Assert.Single(completed.Analysis.Frames);
        Assert.Equal(new Point(100, 200), frame.BallPosition.Value);
    }

    [Fact]
    public async Task Complete_analysis_skips_without_active_session()
    {
        await _Testee.Handle(new CompleteReplayAnalysisRequest(), _Output, CancellationToken.None);

        await _Output.Received().ReportSkipped("No active session.");
    }
}
