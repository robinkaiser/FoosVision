// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.BallFinding.Processing.CircleFinding;

namespace FoosVision.Vision.UnitTests.BallFinding;

public class CirclePartsMergerTests
{
    private readonly CirclePartsMerger _Testee;

    public CirclePartsMergerTests()
    {
        int maxCircleParts = 4;
        int expectedRadius = 8;

        // r = 8 =>
        // MaximumSquaredCirclePartsDistance = r * r / 16 = 4
        // MinimumSquaredFullCircleDistance = r * r * 4 = 256

        _Testee = new(expectedRadius, maxCircleParts);
    }

    [Fact]
    public void Fixture()
    {
        Assert.False(_Testee.MergeCircles(0).Any());
    }

    [Fact]
    public void Single_Circle()
    {
        int count = 0;

        AddPart(1, 10, 3, 1.23, 42, ref count);

        var circles = _Testee.MergeCircles(count);
        Assert.Single(circles);
        CheckCircle(circles.ElementAt(0), 1, 10, 3, 1.23, 42);
    }

    [Fact]
    public void Sort_Circles()
    {
        int count = 0;

        AddPart(1, 100, 1, 1.0, 3, ref count);
        AddPart(3, 300, 3, 3.0, 1, ref count);
        AddPart(2, 200, 2, 2.0, 2, ref count);

        var circles = _Testee.MergeCircles(count);
        Assert.Equal(3, circles.Count());
        CheckCircle(circles.ElementAt(0), 1, 100, 1, 1.0, 3);
        CheckCircle(circles.ElementAt(1), 2, 200, 2, 2.0, 2);
        CheckCircle(circles.ElementAt(2), 3, 300, 3, 3.0, 1);
    }

    [Fact]
    public void Merge()
    {
        int count = 0;

        AddPart(0, 0, 1, 3.0, 5, ref count);
        AddPart(0, 2, 3, 5.0, 3, ref count); // Just merges with first
        AddPart(100, 100, 1, 1.0, 20, ref count);
        AddPart(101, 102, 2, 2.0, 15, ref count); // Nearly merged with third, will be discarded

        var circles = _Testee.MergeCircles(count);
        Assert.Equal(2, circles.Count());
        CheckCircle(circles.ElementAt(0), 100, 100, 1, 1.0, 20);
        CheckCircle(circles.ElementAt(1), 0, 1, 2, 4.0, 8);
    }

    [Fact]
    public void Discard_Too_Close()
    {
        int count = 0;

        AddPart(0, 0, 1, 2.0, 4, ref count); // Will be discarded because too close
        AddPart(15, 5, 2, 1.0, 5, ref count);

        var circles = _Testee.MergeCircles(count);
        Assert.Single(circles);
        CheckCircle(circles.ElementAt(0), 15, 5, 2, 1.0, 5);
    }

    [Fact]
    public void Not_Discarded_Enough_Distance()
    {
        int count = 0;

        AddPart(0, 0, 1, 2.0, 4, ref count);
        AddPart(15, 6, 2, 1.0, 5, ref count);

        var circles = _Testee.MergeCircles(count);
        Assert.Equal(2, circles.Count());
        CheckCircle(circles.ElementAt(0), 15, 6, 2, 1.0, 5);
        CheckCircle(circles.ElementAt(1), 0, 0, 1, 2.0, 4);
    }

    private static void CheckCircle(SCircle circle, int expectedX, int expectedY, int expectedRadius,
        double expectedMeanPointError, int expectedPointCount)
    {
        Assert.Equal(expectedX, circle.X);
        Assert.Equal(expectedY, circle.Y);
        Assert.Equal(expectedRadius, circle.Radius);
        Assert.Equal(expectedMeanPointError, circle.MeanPointError);
        Assert.Equal(expectedPointCount, circle.PointCount);
    }

    private void AddPart(int x, int y, int radius, double meanPointError, int pointCount, ref int count)
    {
        _Testee.CirclePartBuffer[count].X = x;
        _Testee.CirclePartBuffer[count].Y = y;
        _Testee.CirclePartBuffer[count].Radius = radius;
        _Testee.CirclePartBuffer[count].MeanPointError = meanPointError;
        _Testee.CirclePartBuffer[count].PointCount = pointCount;

        count++;
    }
}
