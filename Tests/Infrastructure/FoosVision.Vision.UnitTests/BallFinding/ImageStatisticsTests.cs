// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Vision.BallFinding.Processing;

namespace FoosVision.Vision.UnitTests.BallFinding;

public class ImageStatisticsTests
{
    private const int _Width = 6;
    private const int _Height = 4;

    private readonly byte[] _Gray8Image =
    [
        1,  0,  0,  0,  0,  0,
        0,  1,  0,  0,  0,  255,
        0,  0,  1,  0,  1,  0,
        0,  0,  0,  1,  0,  0,
    ];

    [Fact]
    public void Count_Full_Image()
    {
        var rect = new Rectangle(0, 0, _Width, _Height);
        var count = ImageStatistics.CountNonZeroGray8(_Width, _Gray8Image, rect);

        Assert.Equal(6, count);
    }

    [Fact]
    public void Count_Roi_1()
    {
        var rect = new Rectangle(1, 1, 3, 3);
        var count = ImageStatistics.CountNonZeroGray8(_Width, _Gray8Image, rect);

        Assert.Equal(3, count);
    }

    [Fact]
    public void Count_Roi_2()
    {
        var rect = new Rectangle(3, 1, 2, 2);

        var count = ImageStatistics.CountNonZeroGray8(_Width, _Gray8Image, rect);
        Assert.Equal(1, count);
    }

    [Fact]
    public void Count_Roi_3()
    {
        var rect = new Rectangle(4, 0, 2, 4);
        var count = ImageStatistics.CountNonZeroGray8(_Width, _Gray8Image, rect);

        Assert.Equal(2, count);
    }
}
