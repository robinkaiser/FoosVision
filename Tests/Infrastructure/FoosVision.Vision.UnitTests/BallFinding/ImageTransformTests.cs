// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.BallFinding.Processing;
using FoosVision.Vision.Common;
using FoosVision.Vision.TableScene.Processing.ColoredPlayers;

namespace FoosVision.Vision.UnitTests.BallFinding;

public class ImageTransformTests
{
    [Fact]
    public void Convert_Rgba_To_Gray8_Full_Image_White_Ball()
    {
        int width = 3;
        int height = 3;

        byte[] inputRGBA =
        [ // R    G    B  A
            201, 201, 201, 255,
            201, 201, 201, 255,
            200, 200, 200, 255,

            197, 197, 197, 255,
            198, 198, 198, 255,
            192, 192, 192, 255,

            178, 178, 178, 255,
            225, 160, 160, 255,
            160, 225, 160, 255,
        ];

        byte[] inputColorResponseWhiteBall =
        [ // AcceptedMinY  AcceptedMaxY  0  A
             1,   1,   1, 1, // Ignored
             200, 255, 0, 255,
             200, 255, 0, 255,

             192, 196, 0, 255,
             192, 196, 0, 255,
             192, 196, 0, 255,

             128, 255, 0, 255,
             128, 255, 0, 255,
             128, 255, 0, 255,
        ];

        byte[] expectedOutGray8 =
        [
           0,   // Ignored
           200, // Y = 200, accepted range starts at 200
           0,   // Y = 199, below accepted range

           196, // Y = 196, accepted range ends at 196
           0,   // Y = 197, above accepted range
           0,   // Y = 191, below accepted range

           177, // Y = 177, inside accepted range
           0,   // Y = 178, inside accepted range but matches player color
           0,   // Y = 197, inside accepted range but matches player color
        ];

        byte[] outGray8 = new byte[width * height];
        Rectangle rect = new(0, 0, width, height);
        PlayerColorExclusionContext playerColorExclusion = new(
            true,
            CreateColorModel(225, 160, 160),
            true,
            CreateColorModel(160, 225, 160));

        ImageTransform.ConvertRGBA8888toGray8(width, inputRGBA, inputColorResponseWhiteBall, outGray8,
            rect, 0x01010101, BallColor.White, playerColorExclusion);

        Assert.True(outGray8.SequenceEqual(expectedOutGray8));
    }

    [Fact]
    public void Convert_Rgba_To_Gray8_Region_White_Ball()
    {
        int width = 3;
        int height = 3;

        byte[] inputRGBA =
        [ // R    G    B  A
           201, 201, 201, 255,    201, 201, 201, 255,    201, 201, 201, 255,
           201, 201, 201, 255,    201, 201, 201, 255,    201, 201, 201, 255,
           201, 201, 201, 255,    201, 201, 201, 255,    201, 201, 201, 255,
        ];

        byte[] inputColorResponseWhiteBall =
        [ // AcceptedMinY  AcceptedMaxY  0  A
           200, 200, 200, 255,    200, 200, 200, 255,    200, 200, 200, 255,
           200, 200, 200, 255,    200, 200, 200, 255,    200, 200, 200, 255,
           200, 200, 200, 255,    200, 200, 200, 255,    200, 200, 200, 255,
        ];

        byte[] expectedOutGray8 =
        [
           0, 0,   0,
           0, 200, 200,
           0, 0,   0,
        ];

        byte[] outGray8 = new byte[width * height];
        Rectangle region = new(1, 1, 2, 1);

        ImageTransform.ConvertRGBA8888toGray8(
            width,
            inputRGBA,
            inputColorResponseWhiteBall,
            outGray8,
            region,
            0x01010101,
            BallColor.White,
            new());

        Assert.True(outGray8.SequenceEqual(expectedOutGray8));
    }

    [Fact]
    public void Convert_Yuv420_To_Rgba8888_Region()
    {
        int width = 4;
        int height = 4;
        int yRowStride = 6;
        int uvRowStride = 4;
        int uvPixelStride = 2;

        byte[] inputY = Enumerable.Repeat((byte)16, yRowStride * height).ToArray();
        byte[] inputU = Enumerable.Repeat((byte)128, uvRowStride * 2).ToArray();
        byte[] inputV = Enumerable.Repeat((byte)128, uvRowStride * 2).ToArray();
        byte[] outRGBA = new byte[width * height * 4];
        Rectangle region = new(1, 1, 2, 2);

        for (int y = region.Y; y < region.BottomExclusive; y++)
        {
            for (int x = region.X; x < region.RightExclusive; x++)
            {
                inputY[(y * yRowStride) + x] = 188;
            }
        }

        ImageTransform.ConvertYuv420ToRGBA8888(
            inputY,
            inputU,
            inputV,
            width,
            yRowStride,
            1,
            uvRowStride,
            uvPixelStride,
            uvRowStride,
            uvPixelStride,
            outRGBA,
            region);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int offset = ((y * width) + x) * 4;
                bool isInsideRegion = x >= region.X &&
                    x < region.RightExclusive &&
                    y >= region.Y &&
                    y < region.BottomExclusive;

                byte expectedColor = isInsideRegion ? (byte)200 : (byte)0;
                byte expectedAlpha = isInsideRegion ? (byte)255 : (byte)0;

                Assert.Equal(expectedColor, outRGBA[offset]);
                Assert.Equal(expectedColor, outRGBA[offset + 1]);
                Assert.Equal(expectedColor, outRGBA[offset + 2]);
                Assert.Equal(expectedAlpha, outRGBA[offset + 3]);
            }
        }
    }

    private static BallDetectionColorModel CreateColorModel(byte r, byte g, byte b)
    {
        ColorFeature feature = ColorFeature.FromRgb(r, g, b);

        return new(
            feature.Cb,
            feature.Cr,
            100,
            25 * 25);
    }
}
