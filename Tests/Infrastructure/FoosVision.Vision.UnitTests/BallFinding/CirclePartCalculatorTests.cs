// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.BallFinding.Processing.CircleFinding;
using FoosVision.Vision.Common.Processing;

namespace FoosVision.Vision.UnitTests.BallFinding;

public class CirclePartCalculatorTests
{
    private readonly SCircle[] _Circles;

    public CirclePartCalculatorTests()
    {
        _Circles = new SCircle[1];
    }

    [Fact]
    public void Success()
    {
        CirclePartCalculator testee = new(new CirclePartCalculatorParameters()
        {
            ExpectedRadius = 9,
            MinimumShapePoints = 0,
            MaximumShapePoints = 5,
            MinimumCirclePartPoints = 5,
            RandomSeed = 0,
        });

        EdgePoint[] points =
        [
            new(1, 9),
            new(12, 4),
            new(17, 15),
            new(6, 20),
            new(3, 18),
            new(0, 0),
        ];

        int outIndex = 0;
        testee.ProcessEdges(points, points.Length, _Circles, ref outIndex);

        Assert.Equal(1, outIndex);
        Assert.Equal(9, _Circles[0].X);
        Assert.Equal(12, _Circles[0].Y);
        Assert.Equal(9, _Circles[0].Radius);
    }

    [Fact]
    public void Too_Few_Corners()
    {
        CirclePartCalculator testee = new(new CirclePartCalculatorParameters()
        {
            ExpectedRadius = 9,
            MinimumShapePoints = 0,
            MaximumShapePoints = 12,
            MinimumCirclePartPoints = 5,
            RandomSeed = 0,
        });

        EdgePoint[] points =
        [
            new(1, 9),
            new(12, 4),
            new(17, 15),
            new(6, 20),
        ];

        int outIndex = 0;
        testee.ProcessEdges(points, points.Length, _Circles, ref outIndex);

        Assert.Equal(0, outIndex);
    }

    [Fact]
    public void Bad_Radius()
    {
        CirclePartCalculator testee = new(new CirclePartCalculatorParameters()
        {
            ExpectedRadius = 13,
            MinimumShapePoints = 0,
            MaximumShapePoints = 12,
            MinimumCirclePartPoints = 5,
            RandomSeed = 0,
        });

        EdgePoint[] points =
        [
            new(1, 9),
            new(12, 4),
            new(17, 15),
            new(6, 20),
            new(3, 18),
        ];

        int outIndex = 0;
        testee.ProcessEdges(points, points.Length, _Circles, ref outIndex);

        Assert.Equal(0, outIndex);
    }
}
