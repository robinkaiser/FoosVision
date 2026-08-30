// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Domain.TrackingCore.ValueObjects;

/// <summary>
/// Observed ball
/// </summary>
/// <param name="Position">Ball position</param>
/// <param name="Quality">0..1</param>
/// <param name="QualityLevel">Quality level derived from the raw quality value.</param>
public record class ObservedBall(Point Position, double Quality, ObservationQualityLevel QualityLevel)
{
    public ObservedBall(Point position, double quality)
        : this(position, quality, ObservationQualityThresholds.Default.Classify(quality))
    {
    }
}

public enum ObservationQualityLevel
{
    BelowMinimum,
    LowQuality,
    HighQuality,
    VeryHighQuality,
}

public record class ObservationQualityThresholds
{
    public double MinQuality { get; init; } = 0.15;

    public double HighQuality { get; init; } = 0.50;

    public double VeryHighQuality { get; init; } = 0.70;

    public static readonly ObservationQualityThresholds Default = new();

    public ObservationQualityLevel Classify(double quality)
    {
        if (quality >= VeryHighQuality)
        {
            return ObservationQualityLevel.VeryHighQuality;
        }

        if (quality >= HighQuality)
        {
            return ObservationQualityLevel.HighQuality;
        }

        if (quality >= MinQuality)
        {
            return ObservationQualityLevel.LowQuality;
        }

        return ObservationQualityLevel.BelowMinimum;
    }
}
