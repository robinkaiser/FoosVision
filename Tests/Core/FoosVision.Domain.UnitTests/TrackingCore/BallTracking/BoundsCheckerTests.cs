// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.BallTracking;

namespace FoosVision.Domain.UnitTests.TrackingCore.BallTracking;

public class BoundsCheckerTests
{
    [Fact]
    public void Check_bounds_for_rectangle()
    {
        BoundsChecker testee = new(new Trapezium(
            new Point(101, 101),
            new Point(199, 101),
            new Point(101, 199),
            new Point(199, 199)));

        Assert.True(testee.IsInside(new(150, 150)));
        Assert.False(testee.IsOutside(new(150, 150)));

        Assert.True(testee.IsInside(new(101, 101)));
        Assert.True(testee.IsInside(new(199, 101)));
        Assert.True(testee.IsInside(new(101, 199)));
        Assert.True(testee.IsInside(new(199, 199)));

        Assert.False(testee.IsInside(new(100, 100)));
        Assert.False(testee.IsInside(new(200, 100)));
        Assert.False(testee.IsInside(new(100, 200)));
        Assert.False(testee.IsInside(new(200, 200)));
    }

    [Fact]
    public void Check_bounds_for_trapezium()
    {
        BoundsChecker testee = new(new Trapezium(
            new Point(150, 100),
            new Point(250, 100),
            new Point(100, 300),
            new Point(300, 300)));

        Assert.True(testee.IsOutside(new(125 - 1, 200)));
        Assert.True(testee.IsInside(new(125 + 1, 200)));

        Assert.True(testee.IsOutside(new(275 + 1, 200)));
        Assert.True(testee.IsInside(new(275 - 1, 200)));
    }
}
