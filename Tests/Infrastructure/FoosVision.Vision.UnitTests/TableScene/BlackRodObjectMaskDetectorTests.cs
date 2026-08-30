// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.TableScene.Processing.Common;

namespace FoosVision.Vision.UnitTests.TableScene;

public class BlackRodObjectMaskDetectorTests
{
    private const int _Width = 36;
    private const int _Height = 32;

    [Fact]
    public void DetectBar_Expands_From_Interval_To_Dark_Side_Area()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 7, 6, 7, 5, 20, 20, 20);
        Bar bar = CreateVerticalBar(BarType.A2, 16);
        BlackRodObjectMaskDetector detector = CreateDetector();

        RodBlackObjectMasks result = detector.DetectBar(
            image,
            bar,
            CreateRodIntervals(BarType.A2, new RodObjectInterval(6, 10, 0)),
            CreateRule());

        Assert.Single(result.Rectangles);
        Assert.Equal(new Rectangle(7, 6, 12, 5), result.Rectangles[0]);
    }

    [Fact]
    public void DetectRectangles_Fills_Caller_Field_Output_Buffers()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 7, 6, 7, 5, 20, 20, 20);
        Rectangle[] rectangles = new Rectangle[8];
        RodBlackObjectMaskRange[] rodRanges = new RodBlackObjectMaskRange[8];
        BlackRodObjectMaskDetector detector = CreateDetector();

        int count = detector.DetectRectangles(
            image,
            CreatePlayingField(),
            CreateDetection(CreateRodIntervals(BarType.A2, new RodObjectInterval(6, 10, 0))),
            rectangles,
            rodRanges);

        Assert.Equal(1, count);
        Assert.Equal(new RodBlackObjectMaskRange(BarType.A1, 0, 0), rodRanges[0]);
        Assert.Equal(new RodBlackObjectMaskRange(BarType.A2, 0, 1), rodRanges[1]);
        Assert.Equal(new RodBlackObjectMaskRange(BarType.B3, 1, 0), rodRanges[2]);
        Assert.Equal(new Rectangle(8, 6, 11, 5), rectangles[0]);
    }

    [Fact]
    public void DetectRectangles_Limits_CrossRod_Expansion_By_Half_Neighbor_Distance()
    {
        byte[] image = CreateImage();
        SetRectangle(image, 0, 6, 19, 5, 20, 20, 20);
        Rectangle[] rectangles = new Rectangle[8];
        RodBlackObjectMaskRange[] rodRanges = new RodBlackObjectMaskRange[8];
        BlackRodObjectMaskDetector detector = CreateDetector();

        int count = detector.DetectRectangles(
            image,
            CreatePlayingField(),
            CreateDetection(CreateRodIntervals(BarType.A2, new RodObjectInterval(6, 10, 0))),
            rectangles,
            rodRanges);

        Assert.Equal(1, count);
        Assert.Equal(new Rectangle(8, 6, 11, 5), rectangles[0]);
    }

    private static BlackRodObjectMaskDetector CreateDetector()
        => new(
            _Width,
            _Height,
            new(
                MaskMargin: 0,
                AlongRodExpansionMargin: 0,
                AlongRodAdaptiveExpansionMaxExtra: 0,
                CrossRodAllowedEmptyScans: 0,
                MinimumColumnMatches: 1));

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

    private static BlackRodObjectIntervalDetection CreateDetection(RodBlackObjectIntervals a2Intervals)
        => new(
            [
                CreateRodIntervals(BarType.A1),
                a2Intervals,
                CreateRodIntervals(BarType.B3),
                CreateRodIntervals(BarType.A5),
                CreateRodIntervals(BarType.B5),
                CreateRodIntervals(BarType.A3),
                CreateRodIntervals(BarType.B2),
                CreateRodIntervals(BarType.B1),
            ],
            CreateRule());

    private static RodBlackObjectIntervals CreateRodIntervals(BarType barType, params RodObjectInterval[] intervals)
    {
        int[] sampleX = new int[_Height];
        int[] sampleY = new int[_Height];
        bool[] ignored = new bool[_Height];
        int[] leftY = new int[_Height];
        int[] rightY = new int[_Height];
        bool[] leftValid = new bool[_Height];
        bool[] rightValid = new bool[_Height];
        bool[] matches = new bool[_Height];

        for (int i = 0; i < _Height; i++)
        {
            sampleX[i] = 16;
            sampleY[i] = i;
        }

        return new(
            barType,
            intervals,
            new(sampleX, sampleY, ignored, leftY, rightY, leftValid, rightValid, matches, _Height));
    }

    private static BlackObjectRule CreateRule()
        => new(
            50,
            0.07,
            50,
            0.03,
            0.12,
            40,
            60,
            2,
            5,
            3,
            2);

    private static PlayingField CreatePlayingField(IReadOnlyList<Trapezium>? occlusions = null)
        => new(
            new(
                new Point(0, 0),
                new Point(_Width - 1, 0),
                new Point(0, _Height - 1),
                new Point(_Width - 1, _Height - 1)),
            new(
                CreateVerticalBar(BarType.A1, 4),
                CreateVerticalBar(BarType.A2, 16),
                CreateVerticalBar(BarType.B3, 28),
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
