// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Adapters.Recorder.Game.Orchestration;

public interface IReplayCoordinator
{
    Task RequestReplay(
        Frame triggerFrame,
        Frame anchorFrame,
        Point anchorPosition,
        BallPossession anchorPossession,
        int anchorPossessionTimeMs,
        ReplayTriggerKind triggerKind);
}
