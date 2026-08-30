// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableConfig.Processing.HoughLines;

namespace FoosVision.Vision.UnitTests.TableConfig;

public class LineCoverageScorerTests
{
    private const int _Width = 8;
    private const int _Height = 8;

    [Fact]
    public void Full_Vertical_Line_Covers_All_Bins()
    {
        byte[] image = new byte[_Width * _Height];

        for (int y = 0; y < _Height; y++)
        {
            image[(y * _Width) + 3] = 1;
        }

        HoughLine line = new(new(3, 0), new(3, _Height), 0, 0, 0, _Height);
        var score = LineCoverageScorer.ScoreVertical(image, _Width, _Height, new(0, 0, _Width, _Height), line, 4, 0, 1);

        Assert.Equal(4, score.SupportedBins);
        Assert.Equal(4, score.BinCount);
        Assert.Equal(4, score.LongestSupportedRun);
        Assert.Equal(8, score.EdgePixelCount);
        Assert.Equal(1.0, score.Coverage);
        Assert.Equal(1.0, score.LongestRunCoverage);
    }

    [Fact]
    public void Short_Vertical_Line_Covers_Only_Intersecting_Bins()
    {
        byte[] image = new byte[_Width * _Height];

        for (int y = 0; y < 2; y++)
        {
            image[(y * _Width) + 3] = 1;
        }

        HoughLine line = new(new(3, 0), new(3, _Height), 0, 0, 0, 2);
        var score = LineCoverageScorer.ScoreVertical(image, _Width, _Height, new(0, 0, _Width, _Height), line, 4, 0, 1);

        Assert.Equal(1, score.SupportedBins);
        Assert.Equal(4, score.BinCount);
        Assert.Equal(1, score.LongestSupportedRun);
        Assert.Equal(2, score.EdgePixelCount);
        Assert.Equal(0.25, score.Coverage);
        Assert.Equal(0.25, score.LongestRunCoverage);
    }

    [Fact]
    public void Horizontal_Tolerance_Covers_Nearby_Edges()
    {
        byte[] image = new byte[_Width * _Height];

        for (int y = 0; y < _Height; y++)
        {
            image[(y * _Width) + 4] = 1;
        }

        HoughLine line = new(new(3, 0), new(3, _Height), 0, 0, 0, _Height);
        var score = LineCoverageScorer.ScoreVertical(image, _Width, _Height, new(0, 0, _Width, _Height), line, 4, 1, 1);

        Assert.Equal(4, score.SupportedBins);
        Assert.Equal(8, score.EdgePixelCount);
    }

    [Fact]
    public void Full_Horizontal_Line_Covers_All_Bins()
    {
        byte[] image = new byte[_Width * _Height];

        for (int x = 0; x < _Width; x++)
        {
            image[(3 * _Width) + x] = 1;
        }

        HoughLine line = new(new(0, 3), new(_Width, 3), 0, 0, 0, _Width);
        var score = LineCoverageScorer.ScoreHorizontal(image, _Width, _Height, new(0, 0, _Width, _Height), line, 4, 0, 1);

        Assert.Equal(4, score.SupportedBins);
        Assert.Equal(4, score.BinCount);
        Assert.Equal(4, score.LongestSupportedRun);
        Assert.Equal(8, score.EdgePixelCount);
        Assert.Equal(1.0, score.Coverage);
        Assert.Equal(1.0, score.LongestRunCoverage);
    }

    [Fact]
    public void Short_Horizontal_Line_Covers_Only_Intersecting_Bins()
    {
        byte[] image = new byte[_Width * _Height];

        for (int x = 0; x < 2; x++)
        {
            image[(3 * _Width) + x] = 1;
        }

        HoughLine line = new(new(0, 3), new(_Width, 3), 0, 0, 0, 2);
        var score = LineCoverageScorer.ScoreHorizontal(image, _Width, _Height, new(0, 0, _Width, _Height), line, 4, 0, 1);

        Assert.Equal(1, score.SupportedBins);
        Assert.Equal(4, score.BinCount);
        Assert.Equal(1, score.LongestSupportedRun);
        Assert.Equal(2, score.EdgePixelCount);
        Assert.Equal(0.25, score.Coverage);
        Assert.Equal(0.25, score.LongestRunCoverage);
    }

    [Fact]
    public void Vertical_Tolerance_Covers_Nearby_Horizontal_Edges()
    {
        byte[] image = new byte[_Width * _Height];

        for (int x = 0; x < _Width; x++)
        {
            image[(4 * _Width) + x] = 1;
        }

        HoughLine line = new(new(0, 3), new(_Width, 3), 0, 0, 0, _Width);
        var score = LineCoverageScorer.ScoreHorizontal(image, _Width, _Height, new(0, 0, _Width, _Height), line, 4, 1, 1);

        Assert.Equal(4, score.SupportedBins);
        Assert.Equal(8, score.EdgePixelCount);
    }

    [Fact]
    public void Horizontal_Score_Counts_One_Hit_Per_Column()
    {
        byte[] image = new byte[_Width * _Height];

        for (int y = 0; y < _Height; y++)
        {
            image[(y * _Width) + 1] = 1;
        }

        HoughLine line = new(new(0, 3), new(_Width, 3), 0, 0, 0, _Width);
        var score = LineCoverageScorer.ScoreHorizontal(image, _Width, _Height, new(0, 0, _Width, _Height), line, 4, 4, 2);

        Assert.Equal(0, score.SupportedBins);
        Assert.Equal(1, score.EdgePixelCount);
        Assert.Equal(0.0, score.Coverage);
    }
}
