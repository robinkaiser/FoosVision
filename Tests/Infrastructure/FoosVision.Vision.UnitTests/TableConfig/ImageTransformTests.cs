// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Vision.TableConfig.Processing;

namespace FoosVision.Vision.UnitTests.TableConfig;

public class ImageTransformTests
{
    private const byte _R8_1 = 136; // 5-Bit:  10001
    private const byte _G8_1 = 204; // 6-Bit: 110011
    private const byte _B8_1 = 72;  // 5-Bit:  01001
    private const byte _Y8_1 = 168;

    private const byte _R8_2 = 248; // 5-Bit:  11111
    private const byte _G8_2 = 156; // 6-Bit: 100111
    private const byte _B8_2 = 48;  // 5-Bit:  00110
    private const byte _Y8_2 = 170;

    private const int _Width = 4;
    private const int _Height = 2;

    private readonly byte[] _RgbaImage =
    [
        _R8_1, _G8_1, _B8_1, 255,   _R8_1, _G8_1, _B8_1, 255,   _R8_2, _G8_2, _B8_2, 255,   _R8_1, _G8_1, _B8_1, 255,
        _R8_1, _G8_1, _B8_1, 255,   _R8_2, _G8_2, _B8_2, 255,   _R8_2, _G8_2, _B8_2, 255,   _R8_1, _G8_1, _B8_1, 255,
    ];

    private readonly byte[] _Gray8Image =
    [
        _Y8_1, _Y8_1, _Y8_2, _Y8_1,
        _Y8_1, _Y8_2, _Y8_2, _Y8_1,
    ];

    [Fact]
    public void Convert_Rgba_To_Gray8_Full_Image()
    {
        byte[] outGray8 = new byte[_Width * _Height];
        Rectangle rect = new(0, 0, _Width, _Height);

        ImageTransform.ConvertRGBA8888ToGray8(_Width, _RgbaImage, outGray8, rect);

        Assert.True(outGray8.SequenceEqual(_Gray8Image));
    }

    [Fact]
    public void Convert_Rgba_To_Gray8_Upper_Right_Region()
    {
        byte[] outGray8 = new byte[_Width * _Height];
        Rectangle rect = new(2, 0, 2, 2);

        ImageTransform.ConvertRGBA8888ToGray8(_Width, _RgbaImage, outGray8, rect);

        Assert.Equal(0, outGray8[0]);
        Assert.Equal(0, outGray8[1]);
        Assert.Equal(_Gray8Image[2], outGray8[2]);
        Assert.Equal(_Gray8Image[3], outGray8[3]);
        Assert.Equal(0, outGray8[4]);
        Assert.Equal(0, outGray8[5]);
        Assert.Equal(_Gray8Image[6], outGray8[6]);
        Assert.Equal(_Gray8Image[7], outGray8[7]);
    }

    [Fact]
    public void Convert_Rgba_To_Gray8_Lower_Left_Region()
    {
        byte[] outGray8 = new byte[_Width * _Height];
        Rectangle rect = new(0, 1, 2, 1);

        ImageTransform.ConvertRGBA8888ToGray8(_Width, _RgbaImage, outGray8, rect);

        Assert.Equal(0, outGray8[0]);
        Assert.Equal(0, outGray8[1]);
        Assert.Equal(0, outGray8[2]);
        Assert.Equal(0, outGray8[3]);
        Assert.Equal(_Gray8Image[4], outGray8[4]);
        Assert.Equal(_Gray8Image[5], outGray8[5]);
        Assert.Equal(0, outGray8[6]);
        Assert.Equal(0, outGray8[7]);
    }
}
