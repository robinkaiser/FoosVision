// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Domain.Table.ValueObjects;

public record TableImageScale
{
    // Leonhart table
    public const double GoalAxisLengthMm = 1115.0;
    public const double SideAxisLengthMm = 682.0;

    private const double _MillimetersPerMeter = 1000.0;
    private const double _MetersPerSecondToKilometersPerHour = 3.6;

    private TableImageScale(double goalAxisLengthPx, double sideAxisLengthPx)
    {
        GoalAxisLengthPx = goalAxisLengthPx;
        SideAxisLengthPx = sideAxisLengthPx;
    }

    public double GoalAxisLengthPx { get; }

    public double SideAxisLengthPx { get; }

    public static TableImageScale From(TableConfiguration tableConfiguration)
    {
        Trapezium boundary = tableConfiguration.Field.Boundary;

        Point leftCenter = Midpoint(boundary.UpperLeft, boundary.LowerLeft);
        Point rightCenter = Midpoint(boundary.UpperRight, boundary.LowerRight);
        Point upperCenter = Midpoint(boundary.UpperLeft, boundary.UpperRight);
        Point lowerCenter = Midpoint(boundary.LowerLeft, boundary.LowerRight);

        return new TableImageScale(
            Distance(leftCenter, rightCenter),
            Distance(upperCenter, lowerCenter));
    }

    public double ConvertGoalAxisDistancePxToMm(double distancePx)
        => distancePx * GoalAxisLengthMm / GoalAxisLengthPx;

    public double ConvertSideAxisDistancePxToMm(double distancePx)
        => distancePx * SideAxisLengthMm / SideAxisLengthPx;

    public double ConvertGoalAxisSpeedPxPerSToKmh(double speedPxPerS)
        => ConvertMillimetersPerSecondToKilometersPerHour(ConvertGoalAxisDistancePxToMm(speedPxPerS));

    public double ConvertSideAxisSpeedPxPerSToKmh(double speedPxPerS)
        => ConvertMillimetersPerSecondToKilometersPerHour(ConvertSideAxisDistancePxToMm(speedPxPerS));

    private static double ConvertMillimetersPerSecondToKilometersPerHour(double millimetersPerSecond)
        => millimetersPerSecond / _MillimetersPerMeter * _MetersPerSecondToKilometersPerHour;

    private static Point Midpoint(Point a, Point b)
        => new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

    private static double Distance(Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
