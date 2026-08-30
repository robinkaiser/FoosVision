// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Common.UnitTests.Types;

public class LineTests
{
    [Fact]
    public void Line_properties()
    {
        Line l = new(new(2, 1), new(1, 3));

        Assert.Equal(-1, l.Dx);
        Assert.Equal(2, l.Dy);
        Assert.False(l.IsHorizontal);
        Assert.False(l.IsVertical);
        Assert.Equal(5, l.LengthSquared);
    }

    [Fact]
    public void Horizontal_line()
    {
        Line l = new(new(1, 1), new(2, 1));

        Assert.True(l.IsHorizontal);
        Assert.False(l.IsVertical);
    }

    [Fact]
    public void Vertical_line()
    {
        Line l = new(new(1, 1), new(1, 2));

        Assert.False(l.IsHorizontal);
        Assert.True(l.IsVertical);
    }
}
