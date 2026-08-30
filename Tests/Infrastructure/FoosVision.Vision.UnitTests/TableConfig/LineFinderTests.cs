// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Vision.TableConfig.Processing.HoughLines;

namespace FoosVision.Vision.UnitTests.TableConfig;

public class LineFinderTests
{
    private const int _Width = 8;
    private const int _Height = 8;

    private readonly byte[] _Y8Cross =
    [
        0,  0,  0,  1,  0,  0,  0,  0,
        0,  0,  0,  1,  0,  0,  0,  0,
        1,  1,  1,  1,  1,  1,  1,  1,
        0,  0,  0,  1,  0,  0,  0,  0,
        0,  0,  0,  1,  0,  0,  0,  0,
        0,  0,  0,  1,  0,  0,  0,  0,
        0,  0,  0,  1,  0,  0,  0,  0,
        0,  0,  0,  1,  0,  0,  0,  0,
    ];

    private readonly byte[] _Diagonal =
    [
        1,  0,  0,  0,  0,  0,  0,  0,
        0,  1,  0,  0,  0,  0,  0,  0,
        0,  0,  1,  0,  0,  0,  0,  0,
        0,  0,  0,  1,  0,  0,  0,  0,
        0,  0,  0,  0,  1,  0,  0,  0,
        0,  0,  0,  0,  0,  1,  0,  0,
        0,  0,  0,  0,  0,  0,  1,  0,
        0,  0,  0,  0,  0,  0,  0,  1,
    ];

    private readonly byte[] _NearlyVertical =
    [
        0,  0,  0,  1,  0,  0,  0,  0,
        0,  0,  0,  1,  0,  0,  0,  0,
        0,  0,  0,  0,  1,  0,  0,  0,
        0,  0,  0,  0,  1,  0,  0,  0,
        0,  0,  0,  0,  0,  1,  0,  0,
        0,  0,  0,  0,  0,  1,  0,  0,
        0,  0,  0,  0,  0,  0,  1,  0,
        0,  0,  0,  0,  0,  0,  1,  0,
    ];

    private HoughLine[] _HoughLines;

    public LineFinderTests()
    {
        _HoughLines = new HoughLine[LineFinder.MaxLineCount];
    }

    [Fact]
    public void Horizontal_Line()
    {
        var finder = new HorizontalLineFinder(_Width, _Height, 90, 90, 1.0);
        int count = finder.Find(_Y8Cross, new Rectangle(0, 0, _Width, _Height), 0.9, 0, 0, 0, _HoughLines);

        Assert.Equal(1, count);
        CheckLine(_HoughLines[0], 0, 2, 8, 2);
    }

    [Fact]
    public void Vertical_Line()
    {
        var finder = new VerticalLineFinder(_Width, _Height, 0, 0, 1.0);
        int count = finder.Find(_Y8Cross, new Rectangle(0, 0, _Width, _Height), 0.9, 0, 0, 0, _HoughLines);

        Assert.Equal(1, count);
        CheckLine(_HoughLines[0], 3, 0, 3, 8);
    }

    [Fact]
    public void Nearly_Vertical_Line()
    {
        var finder = new VerticalLineFinder(_Width, _Height, -20, 20, 1.0);
        int count = finder.Find(_NearlyVertical, new Rectangle(0, 0, _Width, _Height), 0.9, 0, 0, 0, _HoughLines);

        Assert.Equal(1, count);
        CheckLine(_HoughLines[0], 3, 0, 6, 8, 0.2);
    }

    [Fact]
    public void Diagonal_Line()
    {
        var finder = new HorizontalLineFinder(_Width, _Height, -60, 60, 0.1);
        int count = finder.Find(_Diagonal, new Rectangle(0, 0, _Width, _Height), 0.9, 0, 0, 0, _HoughLines);

        Assert.Contains(_HoughLines.Take(count), l =>
            l.P0.X == 0 &&
            l.P0.Y == 0 &&
            l.P1.X == 8 &&
            l.P1.Y == 8 &&
            l.Angle == -45 &&
            l.Accumulator == 8);
    }

    private static void CheckLine(HoughLine line, double x0, double y0, double x1, double y1, double epsilon = 0.000000001)
    {
        Assert.Equal(x0, line.P0.X, epsilon);
        Assert.Equal(y0, line.P0.Y, epsilon);
        Assert.Equal(x1, line.P1.X, epsilon);
        Assert.Equal(y1, line.P1.Y, epsilon);
    }
}
