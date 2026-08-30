// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Vision.TableScene;
using FoosVision.Vision.TableScene.Processing;

namespace FoosVision.Vision.UnitTests.TableScene;

public class BackgroundMaskingTests
{
#pragma warning disable IDE1006
    private const uint U = TableSceneModel.RgbaIgnoredPixel;
    private const byte B = (byte)BackgroundPixelState.IgnoredPixel;
#pragma warning restore IDE1006

    [Fact]
    public void Ignore_Inside_Vertical_Channel_Mask()
    {
        int width = 12;
        int height = 8;
        VerticalChannel channel = new(5, 1, 9, 11);

        byte[] expectedImage =
        [
            0, 0, 0, 0, 0, 0, B, B, B, B, 0, 0,
            0, 0, 0, 0, 0, B, B, B, B, B, 0, 0,
            0, 0, 0, 0, 0, B, B, B, B, B, 0, 0,
            0, 0, 0, 0, B, B, B, B, B, B, 0, 0,
            0, 0, 0, 0, B, B, B, B, B, B, B, 0,
            0, 0, 0, B, B, B, B, B, B, B, B, 0,
            0, 0, 0, B, B, B, B, B, B, B, B, 0,
            0, 0, B, B, B, B, B, B, B, B, B, 0,
        ];

        byte[] image = new byte[width * height];
        Array.Clear(image);

        BackgroundMasking.IgnoreInsideVerticalChannelMask(width, height, image, channel);

        Assert.True(image.SequenceEqual(expectedImage));
    }

    [Fact]
    public void Ignore_Outside_Trapezium_Mask_Upper()
    {
        int width = 12;
        int height = 8;
        Trapezium trapezium = new(
            new(-1, 1),
            new(width + 1, 5),
            new(-1, height + 1),
            new(width + 1, height + 1)
        );

        byte[] expectedImage =
        [
            B, B, B, B, B, B, B, B, B, B, B, B,
            B, B, B, B, B, B, B, B, B, B, B, B,
            0, 0, 0, B, B, B, B, B, B, B, B, B,
            0, 0, 0, 0, 0, 0, B, B, B, B, B, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, B, B, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        ];

        byte[] image = new byte[width * height];
        Array.Clear(image);

        BackgroundMasking.IgnoreOutsideTrapeziumMask(width, height, image, trapezium);

        Assert.True(image.SequenceEqual(expectedImage));
    }

    [Fact]
    public void Ignore_Outside_Trapezium_Mask_Lower()
    {
        int width = 12;
        int height = 8;
        Trapezium trapezium = new(
            new(-1, -1),
            new(width + 1, -1),
            new(-1, 7),
            new(width + 1, 5)
        );

        byte[] expectedImage =
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, B, B, B, B, B, B,
            B, B, B, B, B, B, B, B, B, B, B, B,
        ];

        byte[] image = new byte[width * height];
        Array.Clear(image);

        BackgroundMasking.IgnoreOutsideTrapeziumMask(width, height, image, trapezium);

        Assert.True(image.SequenceEqual(expectedImage));
    }

    [Fact]
    public void Ignore_Outside_Trapezium_Mask_Left()
    {
        int width = 12;
        int height = 8;
        Trapezium trapezium = new(
            new(5, -1),
            new(width + 1, -1),
            new(1, height + 1),
            new(width + 1, height + 1)
        );

        byte[] expectedImage =
        [
            B, B, B, B, B, B, 0, 0, 0, 0, 0, 0,
            B, B, B, B, B, 0, 0, 0, 0, 0, 0, 0,
            B, B, B, B, B, 0, 0, 0, 0, 0, 0, 0,
            B, B, B, B, 0, 0, 0, 0, 0, 0, 0, 0,
            B, B, B, B, 0, 0, 0, 0, 0, 0, 0, 0,
            B, B, B, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            B, B, B, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            B, B, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        ];

        byte[] image = new byte[width * height];
        Array.Clear(image);

        BackgroundMasking.IgnoreOutsideTrapeziumMask(width, height, image, trapezium);

        Assert.True(image.SequenceEqual(expectedImage));
    }

    [Fact]
    public void Ignore_Outside_Trapezium_Mask_Right()
    {
        int width = 12;
        int height = 8;
        Trapezium trapezium = new(
            new(-1, -1),
            new(11, -1),
            new(-1, height + 1),
            new(9, height + 1)
        );

        byte[] expectedImage =
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, B, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, B, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, B, B,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, B, B,
        ];

        byte[] image = new byte[width * height];
        Array.Clear(image);

        BackgroundMasking.IgnoreOutsideTrapeziumMask(width, height, image, trapezium);

        Assert.True(image.SequenceEqual(expectedImage));
    }

    [Fact]
    public void Ignore_Outside_Trapezium_Mask_Inside()
    {
        int width = 12;
        int height = 8;
        Trapezium trapezium = new(
            new(5, 1),
            new(11, 5),
            new(1, 7),
            new(9, 5)
        );

        byte[] expectedImage =
        [
            B, B, B, B, B, B, B, B, B, B, B, B,
            B, B, B, B, B, B, B, B, B, B, B, B,
            B, B, B, B, B, B, B, B, B, B, B, B,
            B, B, B, B, 0, 0, B, B, B, B, B, B,
            B, B, B, B, 0, 0, 0, 0, 0, B, B, B,
            B, B, B, 0, 0, 0, 0, 0, 0, 0, B, B,
            B, B, B, 0, 0, 0, B, B, B, B, B, B,
            B, B, B, B, B, B, B, B, B, B, B, B,
        ];

        byte[] image = new byte[width * height];
        Array.Clear(image);

        BackgroundMasking.IgnoreOutsideTrapeziumMask(width, height, image, trapezium);

        Assert.True(image.SequenceEqual(expectedImage));
    }

    [Fact]
    public void Ignore_Inside_Trapezium_Mask()
    {
        int width = 12;
        int height = 8;
        Trapezium trapezium = new(
            new(5, 1),
            new(11, 5),
            new(1, 7),
            new(9, 5)
        );

        byte[] expectedImage =
        [
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, B, B, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, B, B, B, B, B, 0, 0, 0,
            0, 0, 0, B, B, B, B, B, B, B, 0, 0,
            0, 0, 0, B, B, B, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        ];

        byte[] image = new byte[width * height];
        Array.Clear(image);

        BackgroundMasking.IgnoreInsideTrapeziumMask(width, height, image, trapezium);

        Assert.True(image.SequenceEqual(expectedImage));
    }

    [Fact]
    public void Ignore_Inside_Rectangle_Mask_Rgba()
    {
        Rectangle rect = new(1, 1, 2, 3);

        uint[] expectedImage =
        [
            0, 0, 0, 0,
            0, U, U, 0,
            0, U, U, 0,
            0, U, U, 0,
        ];

        byte[] image = new byte[4 * 4 * 4];
        Array.Clear(image);

        BackgroundMasking.IgnoreInsideRectangleRgba(4, 4, image, rect);

        var uintArray = GetUintArrayFromByteArray(image);
        Assert.True(uintArray.SequenceEqual(expectedImage));
    }

    private static uint[] GetUintArrayFromByteArray(byte[] byteArray)
    {
        uint[] result = new uint[byteArray.Length / sizeof(uint)];
        Buffer.BlockCopy(byteArray, 0, result, 0, byteArray.Length);
        return result;
    }
}
