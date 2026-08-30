// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Vision.Common;

public static class Geometry
{
    public static bool TryIntersect(
        Line l1,
        Line l2,
        out Point intersection,
        bool segmentOnly = true)
    {
        intersection = Point.Zero;

        Point p = l1.P0;
        Point r = l1.P1 - l1.P0;

        Point q = l2.P0;
        Point s = l2.P1 - l2.P0;

        double rxs = Cross(r, s);

        // Parallel (or collinear) lines
        if (Math.Abs(rxs) < 1e-10)
        {
            // Optionally handle collinear case here if needed
            return false;
        }

        double t = Cross(q - p, s) / rxs;
        double u = Cross(q - p, r) / rxs;

        if (segmentOnly && (t < 0 || t > 1 || u < 0 || u > 1))
        {
            return false;
        }

        intersection = p + (r * t);

        return true;
    }

    private static double Cross(Point a, Point b) => (a.X * b.Y) - (a.Y * b.X);
}
