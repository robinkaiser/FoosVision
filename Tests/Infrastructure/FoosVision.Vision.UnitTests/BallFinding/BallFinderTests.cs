// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.BallFinding;
using FoosVision.Vision.Common;
using FoosVision.Vision.TableScene.Processing;
using NSubstitute;

namespace FoosVision.Vision.UnitTests.BallFinding;

public class BallFinderTests
{
    private const int _Width = 48;
    private const int _Height = 48;
    private const int _CenterX = 24;
    private const int _CenterY = 24;
    private const int _Radius = 20;

    private readonly BallFinder _Testee;

    public BallFinderTests()
    {
        var ballDetectionContextProvider = Substitute.For<IBallDetectionContextProvider>();
        var responseImage = new byte[_Width * _Height * 4];
        BallColorThresholding.InitializeColorResponse(responseImage, BallColor.White);
        ballDetectionContextProvider.ColorResponse32bpp.Returns(responseImage);
        ballDetectionContextProvider.PlayerColorExclusion.Returns(default(PlayerColorExclusionContext));

        _Testee = new(_Width, _Height, ballDetectionContextProvider, 2);
    }

    [Fact]
    public void Full_White_Ball()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig);

        Assert.Single(balls);
        Assert.Equal(_CenterX, balls[0].Position.X);
        Assert.Equal(_CenterY, balls[0].Position.Y);
        Assert.Equal(1.0, balls[0].Quality);
    }

    [Fact]
    public void Full_White_Ball_Inside_Roi()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig, new Rectangle(4, 4, 40, 40));

        Assert.Single(balls);
        Assert.Equal(_CenterX, balls[0].Position.X);
        Assert.Equal(_CenterY, balls[0].Position.Y);
    }

    [Fact]
    public void Full_White_Ball_Inside_Roi_From_Yuv420()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);
        var (bufferY, bufferU, bufferV) = GetYuv420FromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.DetectYuv420(
            bufferY,
            bufferU,
            bufferV,
            _Width,
            _Height,
            _Width,
            1,
            _Width / 2,
            1,
            _Width / 2,
            1,
            tableConfig,
            new Rectangle(4, 4, 40, 40));

        Assert.Single(balls);
        Assert.Equal(_CenterX, balls[0].Position.X);
        Assert.Equal(_CenterY, balls[0].Position.Y);
    }

    [Fact]
    public void Yuv420_Ball_Outside_Roi()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);
        var (bufferY, bufferU, bufferV) = GetYuv420FromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.DetectYuv420(
            bufferY,
            bufferU,
            bufferV,
            _Width,
            _Height,
            _Width,
            1,
            _Width / 2,
            1,
            _Width / 2,
            1,
            tableConfig,
            new Rectangle(0, 0, 10, 10));

        Assert.Empty(balls);
    }

    [Fact]
    public void Full_White_Ball_Outside_Roi()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig, new Rectangle(0, 0, 10, 10));

        Assert.Empty(balls);
    }

    [Fact]
    public void Roi_Is_Intersected_With_Table_Rectangle()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(3, 3);

        var balls = _Testee.Detect(inRGBAImage, tableConfig, new Rectangle(0, 0, _Width, _Height));

        Assert.Empty(balls);
    }

    [Fact]
    public void Half_White_Ball()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 180);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig);

        Assert.Single(balls);
        Assert.Equal(_CenterX, balls[0].Position.X);
        Assert.Equal(_CenterY, balls[0].Position.Y);
        Assert.True(balls[0].Quality >= 0.5);
    }

    [Fact]
    public void Quarter_White_Ball()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 90);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig);

        Assert.Single(balls);
        Assert.Equal(_CenterX, balls[0].Position.X, 1.0);
        Assert.Equal(_CenterY, balls[0].Position.Y, 1.0);
        Assert.True(balls[0].Quality >= 0.25);
    }

    [Fact]
    public void Full_White_Ball_With_Holes()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);
        ZeroOutPixelsForHoles(inputGray8, 0.1);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig);

        Assert.Single(balls);
        Assert.Equal(_CenterX, balls[0].Position.X, 1.0);
        Assert.Equal(_CenterY, balls[0].Position.Y, 1.0);
        Assert.True(balls[0].Quality > 0.7);
    }

    [Fact]
    public void Full_White_Ball_With__Many_Holes()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);
        ZeroOutPixelsForHoles(inputGray8, 0.3);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig);

        Assert.Single(balls);
        Assert.Equal(_CenterX, balls[0].Position.X, 3.0);
        Assert.Equal(_CenterY, balls[0].Position.Y, 3.0);
        Assert.True(balls[0].Quality > 0.4);
    }

    [Fact]
    public void Full_White_Ball_Cut_In_Two_Halfes()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);

        for (int x = 0; x < _Width; x++)
        {
            inputGray8[(_Height / 2 * _Width) + x] = 0;
        }

        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig);

        Assert.Single(balls);
        Assert.Equal(_CenterX, balls[0].Position.X);
        Assert.Equal(_CenterY, balls[0].Position.Y);
        Assert.Equal(1.0, balls[0].Quality);
    }

    [Fact]
    public void Circle_Parts_Overflow()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX, _CenterY, _Radius, 360);

        for (int x = 0; x < _Width; x++)
        {
            inputGray8[(_Height / 2 * _Width) + x] = 0;
        }

        for (int y = 0; y < _Height; y++)
        {
            inputGray8[(y * _Width) + (_Width / 2)] = 0;
        }

        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig);

        // Low quality because we lost two of the 4 parts die to max 2 parts parameter
        Assert.Single(balls);
        Assert.True(balls[0].Quality < 0.7);
    }

    [Fact]
    public void Full_White_Ball_Off_Center()
    {
        var inputGray8 = CreateGray8FilledCircle(_Width, _Height, _CenterX - 1, _CenterY + 1, _Radius, 360);
        var inRGBAImage = GetRGBAFromGray8(_Width, _Height, inputGray8);
        var tableConfig = GetTableConfig(_Width, _Height);

        var balls = _Testee.Detect(inRGBAImage, tableConfig);

        Assert.Single(balls);
        Assert.Equal(_CenterX - 1, balls[0].Position.X);
        Assert.Equal(_CenterY + 1, balls[0].Position.Y);
        Assert.Equal(1.0, balls[0].Quality);
    }

    private static byte[] CreateGray8FilledCircle(
        int width,
        int height,
        int centerX,
        int centerY,
        int radius,
        double sweepDegrees) // 0..360
    {
        // Clamp to [0, 360] to keep behavior predictable for tests.
        if (sweepDegrees < 0) sweepDegrees = 0;
        if (sweepDegrees > 360) sweepDegrees = 360;

        int r2 = radius * radius;
        var img = new byte[width * height];

        // Fast path: full circle == your original behavior.
        if (sweepDegrees >= 360.0)
        {
            for (int y = 0; y < height; y++)
            {
                int dy = y - centerY;
                int dy2 = dy * dy;

                for (int x = 0; x < width; x++)
                {
                    int dx = x - centerX;
                    int d2 = (dx * dx) + dy2;

                    img[(y * width) + x] = (d2 <= r2) ? (byte)255 : (byte)0;
                }
            }

            return img;
        }

        // Sector / arc path.
        const double RadToDeg = 180.0 / Math.PI;

        for (int y = 0; y < height; y++)
        {
            int dy = y - centerY;
            int dy2 = dy * dy;

            for (int x = 0; x < width; x++)
            {
                int dx = x - centerX;
                int d2 = (dx * dx) + dy2;

                if (d2 > r2)
                {
                    img[(y * width) + x] = 0;
                    continue;
                }

                if (sweepDegrees <= 0.0)
                {
                    img[(y * width) + x] = 0;
                    continue;
                }

                // Angle in degrees: 0..360
                // Use -dy so "up" is positive Y (math convention) while image Y increases downward.
                double angleDeg = Math.Atan2(-dy, dx) * RadToDeg;
                if (angleDeg < 0) angleDeg += 360.0;

                img[(y * width) + x] = (angleDeg <= sweepDegrees) ? (byte)255 : (byte)0;
            }
        }

        return img;
    }

    private static void ZeroOutPixelsForHoles(byte[] img, double percentage)
    {
        int n = img.Length;
        int k = (int)Math.Round(n * percentage, MidpointRounding.AwayFromZero);
        if (k <= 0) return;
        if (k >= n)
        {
            Array.Clear(img, 0, n);
            return;
        }

        // Fisher–Yates partial shuffle on indices to pick k distinct positions without bias.
        var indices = new int[n];
        for (int i = 0; i < n; i++) indices[i] = i;

        var rng = new Random(42);
        for (int i = 0; i < k; i++)
        {
            int j = rng.Next(i, n);   // i..n-1
            (indices[i], indices[j]) = (indices[j], indices[i]);
            img[indices[i]] = 0;
        }
    }

    private static byte[] GetRGBAFromGray8(int width, int height, byte[] gray8)
    {
        var rgbaImage = new byte[width * height * 4];

        for (int i = 0; i < width * height; i++)
        {
            int d = i * 4;
            rgbaImage[d + 0] = gray8[i];
            rgbaImage[d + 1] = gray8[i];
            rgbaImage[d + 2] = gray8[i];
            rgbaImage[d + 3] = gray8[i];
        }

        return rgbaImage;
    }

    private static (byte[] BufferY, byte[] BufferU, byte[] BufferV) GetYuv420FromGray8(int width, int height, byte[] gray8)
    {
        var bufferY = new byte[width * height];
        var bufferU = new byte[(width / 2) * (height / 2)];
        var bufferV = new byte[(width / 2) * (height / 2)];

        Array.Fill(bufferU, (byte)128);
        Array.Fill(bufferV, (byte)128);

        for (int i = 0; i < gray8.Length; i++)
        {
            bufferY[i] = gray8[i] == 0 ? (byte)16 : (byte)235;
        }

        return (bufferY, bufferU, bufferV);
    }

    private static TableConfiguration GetTableConfig(int width, int height)
    {
        var dummyBar = new Bar(
            BarType.A1,
            new Line(new(), new()),
            new Line(new(), new()),
            new Line(new(), new()));

        var tableConfig = new TableConfiguration(
           new PlayingField(
               new Trapezium(
                   new Point(0, 0),
                   new Point(width - 1, 0),
                   new Point(0, height - 1),
                   new Point(width - 1, height - 1)),
               new TableBars(
                   dummyBar,
                   dummyBar,
                   dummyBar,
                   dummyBar,
                   dummyBar,
                   dummyBar,
                   dummyBar,
                   dummyBar),
               Occlusions: []),
           new PlayerColors(0xFFFF0000, 0xFF0000FF),
           BallColor.White);

        return tableConfig;
    }
}
