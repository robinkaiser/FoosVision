// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Entities;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.UseCases.Game.ProcessFrame;

public class ProcessFrameInteractor : IProcessFrameInputPort
{
    private readonly IGameSessionStore _SessionStore;

    public ProcessFrameInteractor(IGameSessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public async Task Handle(ProcessFrameRequest request, IProcessFrameOutputPort output, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out GameSession session))
        {
            await output.ReportSkipped("No active session.");
            return;
        }

        var observed = request.Vision.DetectBalls(session.TableConfig);
        var changes = session.ApplyObservations(request.Frame, observed);

        bool isFound = false;
        Point position = new();
        Vector2 velocity = new();
        IReadOnlyList<TrackedBall> ballCandidates = [];
        BallPossession possession = BallPossession.None;
        int possessionTimeMs = 0;
        bool isTimeFoul = false;
        bool requestTableConfigUpdate = false;
        bool requestTableSceneUpdate = false;
        bool requestReplay = false;
        Frame replayAnchorFrame = new();
        Point replayAnchorPosition = new();
        BallPossession replayAnchorPossession = BallPossession.None;
        int replayAnchorPossessionTimeMs = 0;
        ReplayTriggerKind replayTriggerKind = ReplayTriggerKind.BallDisappeared;

        foreach (var change in changes)
        {
            switch (change)
            {
                case TrackedBallInfo(bool found, BallPossession bp, int pt, bool tf, Point p, Vector2 v, IReadOnlyList<TrackedBall> c):
                    isFound = found;
                    position = p;
                    velocity = v;
                    ballCandidates = c;
                    possession = bp;
                    possessionTimeMs = pt;
                    isTimeFoul = tf;
                    break;

                case UpdateTableConfigRequest:
                    requestTableConfigUpdate = true;
                    break;

                case UpdateTableSceneRequest:
                    requestTableSceneUpdate = true;
                    break;

                case ReplayRequest(Frame anchorFrame, Point anchorPosition, BallPossession anchorPossession, int anchorPossessionTimeMs, ReplayTriggerKind triggerKind):
                    requestReplay = true;
                    replayAnchorFrame = anchorFrame;
                    replayAnchorPosition = anchorPosition;
                    replayAnchorPossession = anchorPossession;
                    replayAnchorPossessionTimeMs = anchorPossessionTimeMs;
                    replayTriggerKind = triggerKind;
                    break;
            }
        }

        ProcessFrameResponse response = new(
            request.Frame,
            isFound,
            position,
            velocity,
            ballCandidates,
            [.. observed],
            possession,
            possessionTimeMs,
            isTimeFoul,
            requestTableConfigUpdate,
            requestTableSceneUpdate,
            requestReplay,
            replayAnchorFrame,
            replayAnchorPosition,
            replayAnchorPossession,
            replayAnchorPossessionTimeMs,
            replayTriggerKind);

        await output.ReportProcessed(response);
    }
}
