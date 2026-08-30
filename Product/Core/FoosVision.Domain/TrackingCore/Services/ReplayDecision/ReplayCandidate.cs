// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.ReplayDecision;

public readonly record struct ReplayCandidate(
    Frame Frame,
    Point Position,
    BallPossession Possession,
    int PossessionTimeMs,
    Vector2 VelocityPxPerS,
    TrackingConfidence Confidence,
    BarType Bar);
