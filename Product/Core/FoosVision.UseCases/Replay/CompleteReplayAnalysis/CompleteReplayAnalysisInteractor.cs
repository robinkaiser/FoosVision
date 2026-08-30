// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.Replay.CompleteReplayAnalysis;

public class CompleteReplayAnalysisInteractor : ICompleteReplayAnalysisInputPort
{
    private readonly IReplaySessionStore _SessionStore;

    public CompleteReplayAnalysisInteractor(IReplaySessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(CompleteReplayAnalysisRequest request, ICompleteReplayAnalysisOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out ReplaySession session))
        {
            await output.ReportSkipped("No active session.");
            return;
        }

        if (!session.CurrentReplayId.TryGetValue(out ReplayId replayId))
        {
            await output.ReportSkipped("No active replay.");
            return;
        }

        var analysis = session.GetAnalysis();

        ReplayAnalysisCompletedResponse response = new(replayId, analysis);

        await output.ReportReplayAnalysisCompleted(response);
    }
}
