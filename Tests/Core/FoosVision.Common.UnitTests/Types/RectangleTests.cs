// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Common.UnitTests.Types;

public class RectangleTests
{
    [Fact]
    public void Intersect_returns_overlap()
    {
        Rectangle a = new(10, 20, 30, 40);
        Rectangle b = new(20, 10, 30, 30);

        Rectangle result = Rectangle.Intersect(a, b);

        Assert.Equal(new Rectangle(20, 20, 20, 20), result);
    }

    [Fact]
    public void Intersect_returns_empty_rectangle_when_rectangles_do_not_overlap()
    {
        Rectangle a = new(0, 0, 10, 10);
        Rectangle b = new(20, 20, 10, 10);

        Rectangle result = Rectangle.Intersect(a, b);

        Assert.True(result.IsEmpty);
        Assert.Equal(0, result.Width);
        Assert.Equal(0, result.Height);
    }
}
