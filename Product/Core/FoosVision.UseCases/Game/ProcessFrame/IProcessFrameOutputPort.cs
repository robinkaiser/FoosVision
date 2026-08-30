// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.UseCases.Game.ProcessFrame;

public record ProcessFrameResponse(
    Frame Frame,
    bool IsBallFound,
    Point BallPosition,
    Vector2 BallVelocityPxPerS,
    IReadOnlyList<TrackedBall> BallCandidates,
    IReadOnlyList<ObservedBall> Observations,
    BallPossession Possession,
    int PossessionTimeMs,
    bool IsTimeFoul,
    bool RequestTableConfigUpdate,
    bool RequestTableSceneUpdate,
    bool RequestReplay,
    Frame ReplayAnchorFrame,
    Point ReplayAnchorPosition,
    BallPossession ReplayAnchorPossession,
    int ReplayAnchorPossessionTimeMs,
    ReplayTriggerKind ReplayTriggerKind);

public interface IProcessFrameOutputPort
{
    Task ReportProcessed(ProcessFrameResponse response);

    Task ReportSkipped(string reason);
}
