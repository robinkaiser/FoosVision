// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.Replay.StartReplayAnalysis;

public class StartReplayAnalysisInteractor : IStartReplayAnalysisInputPort
{
    private readonly IReplaySessionStore _SessionStore;

    public StartReplayAnalysisInteractor(IReplaySessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(StartReplayAnalysisRequest request, IStartReplayAnalysisOutputPort output, CancellationToken ct)
    {
        BallTracker ballTracker = new(BallTrackerParams.Default, request.TableConfiguration);
        ReplayAnalyzer replayAnalyzer = new(TableImageScale.From(request.TableConfiguration));
        ReplaySession session = new(ballTracker, replayAnalyzer);
        _SessionStore.SaveActive(session);

        session.Start(request.ReplayId, request.TrackAnchor, request.TableConfiguration);

        await output.ReportReplayAnalysisStarted(new ReplayAnalysisStartedResponse(request.ReplayId));
    }
}
