// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Replay.ValueObjects;

public record ReplayAnalysisFrame(
    long TimeNs,
    Option<Point> BallPosition,
    BallPossession Possession,
    IReadOnlyList<ReplayMetric> Metrics);
