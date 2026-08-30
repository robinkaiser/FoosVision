// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Common.UnitTests.Types;

public class PointTests
{
    [Fact]
    public void Zero_point()
    {
        var zero = Point.Zero;

        Assert.Equal(0, zero.X);
        Assert.Equal(0, zero.Y);
    }

    [Fact]
    public void Sum_of_two_points()
    {
        Point a = new(1, 4);
        Point b = new(2, 3);
        Point c = a + b;

        Assert.Equal(3, c.X);
        Assert.Equal(7, c.Y);
    }

    [Fact]
    public void Difference_of_two_points()
    {
        Point a = new(1, 4);
        Point b = new(2, 3);
        Point c = a - b;

        Assert.Equal(-1, c.X);
        Assert.Equal(1, c.Y);
    }

    [Fact]
    public void Multiplication_by_scalar_right()
    {
        Point a = new(2, 3);
        Point c = a * 2.5;

        Assert.Equal(5.0, c.X);
        Assert.Equal(7.5, c.Y);
    }

    [Fact]
    public void Multiplication_by_scalar_left()
    {
        Point a = new(2, 3);
        Point c = 2.5 * a;

        Assert.Equal(5.0, c.X);
        Assert.Equal(7.5, c.Y);
    }

    [Fact]
    public void Division_by_scalar()
    {
        Point a = new(5, 7.5);
        Point c = a / 2.5;

        Assert.Equal(2.0, c.X);
        Assert.Equal(3.0, c.Y);
    }

    [Fact]
    public void Division_by_zero_throws()
    {
        Point a = new(1, 1);

        _ = Assert.Throws<DivideByZeroException>(() =>
        {
            _ = a / 0;
        });
    }
}
