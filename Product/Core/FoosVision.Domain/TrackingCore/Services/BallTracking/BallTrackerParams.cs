// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.TrackingCore.Services.BallTracking;

public record class BallTrackerParams
{
    // Max Distances (pixel)
    public int TrackedBallMaxNearByDistanceForObservationMatchingPx { get; init; } = 20;
    public int TrackedBallMaxQuiteNearByDistanceForObservationMatchingPx { get; init; } = 200;
    public int TrackedBallMaxDistanceForCreationBasedOnLowQObservationPx { get; init; } = 200;
    public int TrackedBallMaxReacquisitionDistancePx { get; init; } = 200;

    // TODO: Distinguish between X and Y? X could be smaller since it only needs to cover the width of
    // the bar + figure, whereas in Y (under the bar) a larger prediction distance makes sense
    public int TrackedBallMaxPredictionDistancePx { get; init; } = 250;

    public TimeSpan TrackedBallMaxUnobservedTime { get; init; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan TrackedBallReacquisitionTime { get; init; } = TimeSpan.FromSeconds(2);

    public double LowPassAlphaDeltaXY { get; init; } = 1.0;

    public int MaxTrackedBallsCount { get; init; } = 100;

    public static readonly BallTrackerParams Default = new();
}
