// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Training.Services;

internal static class TrainingGeometry
{
    public static BarType ThreeBar(Team team) => team == Team.A ? BarType.A3 : BarType.B3;
    public static BarType FiveBar(Team team) => team == Team.A ? BarType.A5 : BarType.B5;
    public static BarType TargetTwoBar(Team team) => team == Team.A ? BarType.B2 : BarType.A2;
    public static Team Opponent(Team team) => team == Team.A ? Team.B : Team.A;

    public static double CenterX(TableConfiguration table, BarType bar)
    {
        Line center = table.Field.Bars[bar].Center;
        return (center.P0.X + center.P1.X) / 2;
    }

    // Evaluate the field edges at the relevant rod, including perspective/skew.
    public static double SideFraction(TableConfiguration table, BarType bar, Point position)
    {
        double x = CenterX(table, bar);
        Trapezium boundary = table.Field.Boundary;
        double top = YAt(boundary.UpperLeft, boundary.UpperRight, x);
        double bottom = YAt(boundary.LowerLeft, boundary.LowerRight, x);
        return (position.Y - top) / (bottom - top);
    }

    public static double AttackProgress(TableConfiguration table, Team team, Point position, BarType origin)
    {
        double start = CenterX(table, origin);
        double end = CenterX(table, TargetTwoBar(team));
        return (position.X - start) / (end - start);
    }

    public static double DistanceSquared(Point a, Point b)
        => ((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y));

    private static double YAt(Point a, Point b, double x)
        => a.X == b.X ? (a.Y + b.Y) / 2 : a.Y + (((x - a.X) / (b.X - a.X)) * (b.Y - a.Y));
}
