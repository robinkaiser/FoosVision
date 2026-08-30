// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Domain.TrackingCore.ValueObjects;

public enum TrackingConfidence
{
    /// <summary>
    /// Low confidence
    /// </summary>
    Low,

    /// <summary>
    /// Average confidence
    /// </summary>
    Average,

    /// <summary>
    /// High confidence
    /// </summary>
    High,
}

public enum TrackingStatus
{
    /// <summary>
    /// Confirmed by an observation in the current frame.
    /// </summary>
    Observed,

    /// <summary>
    /// Continued from the previous state without an observation in the current frame.
    /// </summary>
    Predicted,
}

public enum TrackingEvidence
{
    /// <summary>
    /// Confirmed by a low-quality observation.
    /// </summary>
    LowQualityObservation,

    /// <summary>
    /// Confirmed by a high-quality observation.
    /// </summary>
    HighQualityObservation,

    /// <summary>
    /// Confirmed by a very-high-quality observation.
    /// </summary>
    VeryHighQualityObservation,

    /// <summary>
    /// Continued from the previous state without an observation.
    /// </summary>
    Prediction,
}

/// <summary>
/// A tracked ball
/// </summary>
/// <param name="Id">Identification of the tracked ball</param>
/// <param name="Frame">Frame</param>
/// <param name="Position">Ball position</param>
/// <param name="Confidence">Confidence in the tracked ball</param>
/// <param name="Status">Tracking status</param>
/// <param name="VelocityPxPerS">Ball velocity in pixels per seconds</param>
public record class TrackedBall(int Id,
    Frame Frame,
    Point Position,
    TrackingConfidence Confidence,
    TrackingStatus Status,
    Vector2 VelocityPxPerS)
{
    public TrackingEvidence Evidence { get; init; }

    public int UnobservedAgeMs { get; init; }
}
