// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Training.Events;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public static class TrainingReplayRules
{
    private const long _LeadNs = 300L * 1_000_000L;
    private const long _TailNs = 300L * 1_000_000L;

    public static ReplayPlan Create(TrainingAttemptCompleted completed)
    {
        TrainingAnchor anchor = completed.Attempt.Anchor;
        return new(anchor.Frame, anchor.Position, anchor.Possession, 0,
            Math.Max(0, anchor.Frame.TimestampNs - _LeadNs),
            completed.ConfirmationFrame.TimestampNs + _TailNs, 0.25, 1);
    }
}
