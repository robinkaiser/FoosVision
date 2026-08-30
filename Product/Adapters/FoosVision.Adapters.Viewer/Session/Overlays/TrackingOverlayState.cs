// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Adapters.Viewer.Session.Overlays;

public record TrackingOverlayMetric(string Name, double Value, string Unit);

public record ObservationOverlayPoint(OverlayPoint Position, ObservationQualityLevel QualityLevel);

public record BallCandidateOverlayPoint(OverlayPoint Position, TrackingStatus Status, TrackingConfidence Confidence);

public record TrackingOverlayState(
    IReadOnlyList<OverlayPoint> Trail,
    OverlayPoint? BallPosition,
    IReadOnlyList<ObservationOverlayPoint> Observations,
    IReadOnlyList<BallCandidateOverlayPoint> BallCandidates,
    Team PossessingTeam,
    PossessionArea PossessionArea,
    int PossessionTimeMs,
    bool IsTimeFoul,
    IReadOnlyList<TrackingOverlayMetric> Metrics);
