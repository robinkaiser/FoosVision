// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Replay.ValueObjects;

public enum ReplayTrackedFrameStatus
{
    Anchor,
    Tracked,
    Predicted,
    Missing,
}

public record ReplayTrackedFrame(
    long TimeNs,
    Point BallPosition,
    BallPossession Possession,
    ReplayTrackedFrameStatus Status,
    Vector2 VelocityPxPerS);
