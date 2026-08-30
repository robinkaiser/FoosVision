// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Common.Types;

public readonly record struct Rectangle(int X, int Y, int Width, int Height)
{
    public int RightExclusive => X + Width;

    public int BottomExclusive => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static Rectangle Intersect(Rectangle a, Rectangle b)
    {
        int x = Math.Max(a.X, b.X);
        int y = Math.Max(a.Y, b.Y);
        int rightExclusive = Math.Min(a.RightExclusive, b.RightExclusive);
        int bottomExclusive = Math.Min(a.BottomExclusive, b.BottomExclusive);

        return new Rectangle(
            x,
            y,
            Math.Max(0, rightExclusive - x),
            Math.Max(0, bottomExclusive - y));
    }
}
