// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.ReplayDecision;

public enum ReplayTriggerKind
{
    BallDisappeared,
    SavedShot,
}

public record ReplayAnchor(
    Frame Frame,
    Point Position,
    BallPossession Possession,
    int PossessionTimeMs,
    ReplayTriggerKind TriggerKind);
