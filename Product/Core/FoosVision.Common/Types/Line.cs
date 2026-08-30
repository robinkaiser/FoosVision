// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Types;

public readonly record struct Line(Point P0, Point P1)
{
    public double Dx => P1.X - P0.X;

    public double Dy => P1.Y - P0.Y;

    public bool IsHorizontal => P0.Y == P1.Y;

    public bool IsVertical => P0.X == P1.X;

    public double LengthSquared => (Dx * Dx) + (Dy * Dy);
}
