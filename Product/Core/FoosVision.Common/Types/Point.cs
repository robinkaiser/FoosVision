// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Types;

public readonly record struct Point(double X, double Y)
{
    public static Point Zero => default;

    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);

    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);

    // Scalar multiplication
    public static Point operator *(Point p, double s) => new(p.X * s, p.Y * s);

    public static Point operator *(double s, Point p) => p * s;

    // Scalar division
    public static Point operator /(Point p, double s)
    {
        if (s == 0) throw new DivideByZeroException();

        return new(p.X / s, p.Y / s);
    }
}
