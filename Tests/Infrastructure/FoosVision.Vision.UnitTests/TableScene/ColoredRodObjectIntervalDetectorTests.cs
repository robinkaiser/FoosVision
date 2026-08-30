// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.UnitTests.TableScene;

public class ColoredRodObjectIntervalDetectorTests
{
    private const int _Width = 24;
    private const int _Height = 7;

    [Fact]
    public void Detect_Bar_Finds_Colored_Object_Intervals()
    {
        byte[] image = CreateImage();
        Bar bar = CreateHorizontalBar(BarType.A2);
        ColoredRodObjectIntervalDetector detector = new(_Width, _Height, new ColoredRodObjectIntervalDetectionOptions(
            EdgeWindowLength: 1,
            EdgePeakNeighborhood: 1,
            EdgePairMaximumDistance: 8));

        RodColoredObjectIntervals intervals = detector.DetectBar(image, bar);

        Assert.Equal(2, intervals.Intervals.Count);

        Assert.Equal(new RodObjectInterval(5, 8, intervals.Intervals[0].Score), intervals.Intervals[0]);
        Assert.Equal(new RodObjectInterval(16, 19, intervals.Intervals[1].Score), intervals.Intervals[1]);
    }

    [Fact]
    public void Detect_Bar_Keeps_Additional_Object_Intervals()
    {
        byte[] image = CreateImage();
        SetRodSection(image, 21, 21, 255, 0, 0);

        Bar bar = CreateHorizontalBar(BarType.A2);
        ColoredRodObjectIntervalDetector detector = new(
            _Width,
            _Height,
            new ColoredRodObjectIntervalDetectionOptions(
                EdgeWindowLength: 1,
                EdgePeakNeighborhood: 1,
                EdgePairMaximumDistance: 8));

        RodColoredObjectIntervals intervals = detector.DetectBar(image, bar);

        Assert.Equal(2, intervals.Intervals.Count);
        Assert.DoesNotContain(intervals.Intervals, interval => interval.StartIndex == 21);
    }

    [Fact]
    public void Detect_Field_Skips_Occluded_Rod_Windows()
    {
        byte[] image = CreateImage();
        PlayingField field = CreatePlayingField(
            [
                new(
                    new Point(4, 2.7),
                    new Point(10, 2.7),
                    new Point(4, 3.3),
                    new Point(10, 3.3)),
            ]);

        ColoredRodObjectIntervalDetector detector = new(_Width, _Height, new ColoredRodObjectIntervalDetectionOptions(
            EdgeWindowLength: 1,
            EdgePeakNeighborhood: 1,
            EdgePairMaximumDistance: 8));

        ColoredRodObjectIntervalDetection detection = detector.Detect(image, field);

        RodColoredObjectIntervals intervals = detection.Rods[1];

        Assert.Equal(BarType.A2, intervals.BarType);
        Assert.Empty(intervals.Intervals);

        for (int i = 4; i <= 10; i++)
        {
            Assert.True(intervals.SampleProfile.Occluded[i]);
            Assert.Equal(0, intervals.EdgeScoreProfile.Scores[i]);
        }
    }

    [Fact]
    public void Detect_Field_Extrapolates_Occlusions_To_Outer_Rods()
    {
        byte[] image = CreateImage();
        Bar outerBar = CreateVerticalBar(BarType.A1);
        PlayingField field = CreatePlayingField(
            outerBar,
            [
                new(
                    new Point(10, 2.7),
                    new Point(12, 2.7),
                    new Point(10, 3.3),
                    new Point(12, 3.3)),
            ]);

        ColoredRodObjectIntervalDetector detector = new(_Width, _Height, new ColoredRodObjectIntervalDetectionOptions(
            EdgeWindowLength: 1,
            EdgePeakNeighborhood: 1,
            EdgePairMaximumDistance: 8));

        ColoredRodObjectIntervalDetection detection = detector.Detect(image, field);

        RodColoredObjectIntervals intervals = detection.Rods[0];

        Assert.Equal(BarType.A1, intervals.BarType);
        Assert.Equal(3, intervals.SampleProfile.X[3]);
        Assert.Equal(3, intervals.SampleProfile.Y[3]);
        Assert.True(intervals.SampleProfile.Occluded[3]);
        Assert.Equal(0, intervals.EdgeScoreProfile.Scores[3]);
    }

    private static byte[] CreateImage()
    {
        byte[] image = new byte[_Width * _Height * 4];

        for (int y = 0; y < _Height; y++)
        {
            for (int x = 0; x < _Width; x++)
            {
                SetPixel(image, x, y, 30, 80, 30);
            }
        }

        SetRodSection(image, 0, _Width - 1, 200, 200, 200);
        SetRodSection(image, 5, 7, 20, 160, 40);
        SetRodSection(image, 16, 18, 20, 160, 40);

        return image;
    }

    private static void SetRodSection(byte[] image, int startX, int endX, byte r, byte g, byte b)
    {
        for (int y = 2; y <= 4; y++)
        {
            for (int x = startX; x <= endX; x++)
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

    private static Bar CreateHorizontalBar(BarType type)
        => new(
            type,
            new Line(new Point(0, 2), new Point(_Width - 1, 2)),
            new Line(new Point(0, 3), new Point(_Width - 1, 3)),
            new Line(new Point(0, 4), new Point(_Width - 1, 4)));

    private static PlayingField CreatePlayingField(IReadOnlyList<Trapezium> occlusions)
        => CreatePlayingField(CreateHorizontalBar(BarType.A1), occlusions);

    private static PlayingField CreatePlayingField(Bar a1Bar, IReadOnlyList<Trapezium> occlusions)
        => new(
            new(
                new Point(0, 0),
                new Point(_Width - 1, 0),
                new Point(0, _Height - 1),
                new Point(_Width - 1, _Height - 1)),
            new(
                a1Bar,
                CreateHorizontalBar(BarType.A2),
                CreateHorizontalBar(BarType.B3),
                CreateHorizontalBar(BarType.A5),
                CreateHorizontalBar(BarType.B5),
                CreateHorizontalBar(BarType.A3),
                CreateHorizontalBar(BarType.B2),
                CreateHorizontalBar(BarType.B1)),
            occlusions);

    private static Bar CreateVerticalBar(BarType type)
        => new(
            type,
            new Line(new Point(2, 0), new Point(2, _Height - 1)),
            new Line(new Point(3, 0), new Point(3, _Height - 1)),
            new Line(new Point(4, 0), new Point(4, _Height - 1)));
}
