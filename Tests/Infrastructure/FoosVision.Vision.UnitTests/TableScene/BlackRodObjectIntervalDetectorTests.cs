// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.UnitTests.TableScene;

public class BlackRodObjectIntervalDetectorTests
{
    private const int _Width = 36;
    private const int _Height = 32;

    [Fact]
    public void Detect_Finds_Dark_SideBand_Intervals()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 7, 6, 5, 5, 20, 20, 20);
        BlackRodObjectIntervalDetector detector = CreateDetector();

        BlackRodObjectIntervalDetection result = detector.Detect(image, CreatePlayingField(), CreateEmptyIgnoredMasks(), true);

        Assert.Equal(20, result.Rule.MaximumObjectY);
        Assert.Equal(0.005, result.Rule.ObjectPercentile);
        Assert.Single(result.Rods[1].Intervals);
        Assert.Equal(new(6, 10, 0), result.Rods[1].Intervals[0]);
    }

    [Fact]
    public void Detect_Ignores_Colored_Player_Masks()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 7, 6, 5, 5, 20, 20, 20);
        SetRectangle(image, 35, 0, 1, _Height, 20, 20, 20);
        BlackRodObjectIntervalDetector detector = CreateDetector();
        IReadOnlyList<RodObjectMask> ignoredMasks = CreateIgnoredMasks(
            new Rectangle(7, 6, 5, 5));

        BlackRodObjectIntervalDetection result = detector.Detect(image, CreatePlayingField(), ignoredMasks, true);

        Assert.Empty(result.Rods[1].Intervals);
    }

    [Fact]
    public void Detect_Ignores_Occlusions()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 7, 6, 5, 5, 20, 20, 20);
        SetRectangle(image, 35, 0, 1, _Height, 20, 20, 20);
        BlackRodObjectIntervalDetector detector = CreateDetector();
        PlayingField field = CreatePlayingField(
            [
                new(
                    new Point(0, 5),
                    new Point(_Width - 1, 5),
                    new Point(0, 11),
                    new Point(_Width - 1, 11)),
            ]);

        BlackRodObjectIntervalDetection result = detector.Detect(image, field, CreateEmptyIgnoredMasks(), true);

        Assert.Empty(result.Rods[1].Intervals);
    }

    [Fact]
    public void Detect_Uses_OneColor_Percentile_Window_When_Only_One_Color_Model_Exists()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 7, 0, 5, 32, 20, 20, 20);
        BlackRodObjectIntervalDetector detector = new(
            _Width,
            _Height,
            new(
                SideBandOffset: 3,
                SideBandWidth: 5,
                OneColoredTeamObjectPercentile: 0.2,
                TwoColoredTeamsObjectPercentile: 0.005,
                MinimumRunLength: 3,
                MaximumGapLength: 0));

        BlackRodObjectIntervalDetection result = detector.Detect(image, CreatePlayingField(), CreateEmptyIgnoredMasks(), false);

        Assert.Equal(0.2, result.Rule.ObjectPercentile);
        Assert.Equal(120, result.Rule.PercentileObjectY);
        Assert.Equal(119, result.Rule.MaximumObjectY);
    }

    [Fact]
    public void Detect_Samples_Only_Field_Side_For_Goalie_Rods()
    {
        byte[] image = CreateImage();
        BlackRodObjectIntervalDetector detector = CreateDetector();

        BlackRodObjectIntervalDetection result = detector.Detect(image, CreatePlayingField(), CreateEmptyIgnoredMasks(), true);

        Assert.False(HasValidSamples(result.Rods[0].SampleProfile.LeftValid, result.Rods[0].SampleProfile.Count));
        Assert.True(HasValidSamples(result.Rods[0].SampleProfile.RightValid, result.Rods[0].SampleProfile.Count));
        Assert.True(HasValidSamples(result.Rods[7].SampleProfile.LeftValid, result.Rods[7].SampleProfile.Count));
        Assert.False(HasValidSamples(result.Rods[7].SampleProfile.RightValid, result.Rods[7].SampleProfile.Count));
    }

    private static BlackRodObjectIntervalDetector CreateDetector()
        => new(
            _Width,
            _Height,
            new(
                SideBandOffset: 3,
                SideBandWidth: 5,
                TwoColoredTeamsObjectPercentile: 0.005,
                MinimumRunLength: 3,
                MaximumGapLength: 0));

    private static byte[] CreateImage()
    {
        byte[] image = new byte[_Width * _Height * 4];

        for (int y = 0; y < _Height; y++)
        {
            for (int x = 0; x < _Width; x++)
            {
                SetPixel(image, x, y, 120, 120, 120);
            }
        }

        return image;
    }

    private static void SetRectangle(byte[] image, int x0, int y0, int width, int height, byte r, byte g, byte b)
    {
        for (int y = y0; y < y0 + height; y++)
        {
            for (int x = x0; x < x0 + width; x++)
            {
                SetPixel(image, x, y, r, g, b);
            }
        }
    }

    private static void SetPixel(byte[] image, int x, int y, byte r, byte g, byte b)
    {
        int offset = ((y * _Width) + x) * 4;

        image[offset + 0] = r;
        image[offset + 1] = g;
        image[offset + 2] = b;
        image[offset + 3] = byte.MaxValue;
    }

    private static IReadOnlyList<RodObjectMask> CreateEmptyIgnoredMasks()
        => CreateIgnoredMasks();

    private static IReadOnlyList<RodObjectMask> CreateIgnoredMasks(params Rectangle[] a2Rectangles)
        =>
            [
                new(BarType.A1, []),
                new(BarType.A2, a2Rectangles),
                new(BarType.B3, []),
                new(BarType.A5, []),
                new(BarType.B5, []),
                new(BarType.A3, []),
                new(BarType.B2, []),
                new(BarType.B1, []),
            ];

    private static bool HasValidSamples(bool[] valid, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (valid[i])
            {
                return true;
            }
        }

        return false;
    }

    private static PlayingField CreatePlayingField(IReadOnlyList<Trapezium>? occlusions = null)
        => new(
            new(
                new Point(0, 0),
                new Point(_Width - 1, 0),
                new Point(0, _Height - 1),
                new Point(_Width - 1, _Height - 1)),
            new(
                CreateVerticalBar(BarType.A1, 30),
                CreateVerticalBar(BarType.A2, 16),
                CreateVerticalBar(BarType.B3, 30),
                CreateVerticalBar(BarType.A5, 30),
                CreateVerticalBar(BarType.B5, 30),
                CreateVerticalBar(BarType.A3, 30),
                CreateVerticalBar(BarType.B2, 30),
                CreateVerticalBar(BarType.B1, 30)),
            occlusions ?? []);

    private static Bar CreateVerticalBar(BarType type, int centerX)
        => new(
            type,
            new Line(new Point(centerX - 2, 0), new Point(centerX - 2, _Height - 1)),
            new Line(new Point(centerX, 0), new Point(centerX, _Height - 1)),
            new Line(new Point(centerX + 2, 0), new Point(centerX + 2, _Height - 1)));
}
