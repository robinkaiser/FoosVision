// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Domain.TrackingCore.Services.BallTracking;

public class BoundsChecker
{
    private readonly Point[] _Poly;

    public BoundsChecker(Trapezium trapezium)
    {
        _Poly = [
            trapezium.UpperLeft,
            trapezium.UpperRight,
            trapezium.LowerRight,
            trapezium.LowerLeft];
    }

    public bool IsInside(Point p)
    {
        bool? sign = null;
        for (int i = 0; i < _Poly.Length; i++)
        {
            var a = _Poly[i];
            var b = _Poly[(i + 1) % _Poly.Length];

            // 2D cross product (edge AB) x (AP)
            double cross = ((b.X - a.X) * (p.Y - a.Y)) - ((b.Y - a.Y) * (p.X - a.X));

            if (cross == 0.0)
            {   // On the edge: treat as inside
                continue;
            }

            bool positive = cross > 0;

            if (sign == null)
            {   // Set reference orientation
                sign = positive;
                continue;
            }

            if (positive != sign)
            {   // Different side → outside
                return false;
            }
        }

        return true;
    }

    public bool IsOutside(Point p)
        => !IsInside(p);
}
