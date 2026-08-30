// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.BallFinding.Processing.CircleFinding;

namespace FoosVision.Vision.UnitTests.BallFinding;

public class CircleModelSolverTests
{
    private record Point(int X, int Y);

    private readonly CircleModelSolver _Testee;

    public CircleModelSolverTests()
    {
        _Testee = new();
    }

    [Fact]
    public void Four_Perfect_Points()
    {
        double delta = 0.00001;

        var points = GetArray(
            new(1, 9),
            new(12, 4),
            new(17, 15),
            new(6, 20));

        var (x, y, rsquared) = _Testee.FitCircle(points.Length, points);

        Assert.Equal(9.0, x, delta);
        Assert.Equal(12.0, y, delta);
        Assert.True(Math.Abs(8.54 - Math.Sqrt(rsquared)) < 0.01);
    }

    [Fact]
    public void Coope_Points()
    {   // Point data from:
        // I.D. Coope's paper, "Circle fitting by linear and nonlinear least squares"
        // published in Journal of Optimization Theory and Applications (1993)
        // Scaled by 10 and shifted by 100

        double delta = 0.001;

        var points = GetArray(
            new(100 + 7, 100 + 40),
            new(100 + 33, 100 + 47),
            new(100 + 56, 100 + 40),
            new(100 + 75, 100 + 13),
            new(100 + 64, 100 - 11),
            new(100 + 44, 100 - 30),
            new(100 + 3, 100 - 25),
            new(100 - 11, 100 + 13));

        var (x, y, rsquared) = _Testee.FitCircle(points.Length, points);

        Assert.Equal(100 + 30.6030, x, delta);
        Assert.Equal(100 + 7.4361, y, delta);
        Assert.True(Math.Abs(41.0914 - Math.Sqrt(rsquared)) < delta);

        // Add Oulier
        Array.Resize(ref points, points.Length + 1);
        points[^1].X = 100 + 30;
        points[^1].Y = 100 + 10;

        var (x2, y2, rsquared2) = _Testee.FitCircle(points.Length, points);

        Assert.Equal(100 + 31.0253, x2, delta);
        Assert.Equal(100 + 7.5467, y2, delta);
        Assert.True(Math.Abs(38.7132 - Math.Sqrt(rsquared2)) < delta);
    }

    private static SPoint[] GetArray(params Point[] points)
    {
        var pointArray = new SPoint[points.Length];
        int i = 0;

        foreach (var point in points)
        {
            pointArray[i].X = point.X;
            pointArray[i].Y = point.Y;
            i++;
        }

        return pointArray;
    }
}
