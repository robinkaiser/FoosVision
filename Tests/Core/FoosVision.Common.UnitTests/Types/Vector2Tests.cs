// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Common.UnitTests.Types;

public class Vector2Tests
{
    [Fact]
    public void Zero_vector()
    {
        var zero = Vector2.Zero;

        Assert.Equal(0, zero.X);
        Assert.Equal(0, zero.Y);
    }

    [Fact]
    public void Sum_of_two_vectors()
    {
        Vector2 a = new(1, 4);
        Vector2 b = new(2, 3);
        Vector2 c = a + b;

        Assert.Equal(3, c.X);
        Assert.Equal(7, c.Y);
    }

    [Fact]
    public void Difference_of_two_vectors()
    {
        Vector2 a = new(1, 4);
        Vector2 b = new(2, 3);
        Vector2 c = a - b;

        Assert.Equal(-1, c.X);
        Assert.Equal(1, c.Y);
    }
}
