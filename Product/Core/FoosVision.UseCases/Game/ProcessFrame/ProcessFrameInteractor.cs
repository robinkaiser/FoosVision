// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Entities;
using FoosVision.Domain.Replay.ValueObjects;
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
        ReplayPlan? replay = null;
        Frame? returnToLiveReplayFrame = null;

        foreach (var change in changes)
        {
            switch (change)
            {
                case ReturnToLiveRequest returnToLive:
                    returnToLiveReplayFrame = returnToLive.ReplayTriggerFrame;
                    break;

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

                case ReplayRequest replayRequest:
                    replay = replayRequest.Replay;
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
            replay,
            session.Id,
            returnToLiveReplayFrame);

        await output.ReportProcessed(response);
    }
}
