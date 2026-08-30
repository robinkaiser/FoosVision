// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.UnitTests.TableScene;

public class BresenhamTests
{
    [Fact]
    public void Horizontal_Line()
    {
        BLine line = new(new(2, 3), new(5, 3));
        var points = new BPoint[12];

        List<BPoint> expectedPoints =
        [
            new(2, 3),
            new(3, 3),
            new(4, 3),
            new(5, 3)
        ];

        var count = Bresenham.GetPoints(line, points);

        Assert.Equal(expectedPoints.Count, count);
        Assert.True(expectedPoints.SequenceEqual(points.Take(count)));
    }

    [Fact]
    public void Vertical_Line()
    {
        BLine line = new(new(2, 3), new(2, 4));
        var points = new BPoint[12];

        List<BPoint> expectedPoints =
        [
            new(2, 3),
            new(2, 4),
        ];

        var count = Bresenham.GetPoints(line, points);

        Assert.Equal(expectedPoints.Count, count);
        Assert.True(expectedPoints.SequenceEqual(points.Take(count)));
    }

    [Fact]
    public void Diagonal_Line_1()
    {
        BLine line = new(new(2, 3), new(5, 4));
        var points = new BPoint[12];

        List<BPoint> expectedPoints =
        [
            new(2, 3),
            new(3, 3),
            new(4, 4),
            new(5, 4)
        ];

        var count = Bresenham.GetPoints(line, points);

        Assert.Equal(expectedPoints.Count, count);
        Assert.True(expectedPoints.SequenceEqual(points.Take(count)));
    }

    [Fact]
    public void Diagonal_Line_2()
    {
        BLine line = new(new(2, 3), new(4, 6));
        var points = new BPoint[12];

        List<BPoint> expectedPoints =
        [
            new(2, 3),
            new(3, 4),
            new(3, 5),
            new(4, 6)
        ];

        var count = Bresenham.GetPoints(line, points);

        Assert.Equal(expectedPoints.Count, count);
        Assert.True(expectedPoints.SequenceEqual(points.Take(count)));
    }

    [Fact]
    public void Diagonal_Line_Bounding_Rect()
    {
        var points = new BPoint[12];
        BLine line = new(new(13, 0), new(8, 11));

        List<BPoint> expectedPoints =
        [
            new(13, 0),
            new(13, 1),
            new(12, 2),
            new(12, 3),
            new(11, 4),
            new(11, 5),
            new(10, 6),
            new(10, 7),
            new(9, 8),
            new(9, 9),
            new(8, 10),
            new(8, 11)
        ];

        var count = Bresenham.GetPoints(line, points);

        Assert.Equal(expectedPoints.Count, count);
        Assert.True(expectedPoints.SequenceEqual(points.Take(count)));
    }
}
