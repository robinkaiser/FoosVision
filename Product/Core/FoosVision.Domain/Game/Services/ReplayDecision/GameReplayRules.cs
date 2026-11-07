// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.ValueObjects;

namespace FoosVision.Domain.Game.Services.ReplayDecision;

public static class GameReplayRules
{
    public static ReplayPlan Create(ReplayAnchor anchor)
    {
        long durationNs = (anchor.TriggerKind == ReplayTriggerKind.SavedShot ? 700L : 1000L) * 1_000_000L;
        return new(anchor.Frame, anchor.Position, anchor.Possession, anchor.PossessionTimeMs,
            anchor.Frame.TimestampNs, anchor.Frame.TimestampNs + durationNs, 0.25, 1);
    }
}
