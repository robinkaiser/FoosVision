// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Vision.TableScene;
using FoosVision.Vision.TableScene.Processing;

namespace FoosVision.Vision.UnitTests.TableScene;

public class BallColorThresholdingTests
{
    [Fact]
    public void Initialize_Color_Response_White()
    {
        byte[] outColorResponse32bpp = new byte[2 * 4];

        byte[] expectedColorResponse32bpp =
        [ // AcceptedMinY  AcceptedMaxY  0  A
             128,          255,          0, 255,
             128,          255,          0, 255,
        ];

        BallColorThresholding.InitializeColorResponse(outColorResponse32bpp, BallColor.White);

        Assert.True(outColorResponse32bpp.SequenceEqual(expectedColorResponse32bpp));
    }

    [Fact]
    public void Compute_Ball_Color_Thresholds_White()
    {
        int width = 2;
        int height = 3;

        byte[] inputBackgroundState =
        [
            (byte)BackgroundPixelState.Ok, (byte)BackgroundPixelState.NotInitialized,
            (byte)BackgroundPixelState.Ok, (byte)BackgroundPixelState.Ok,
            (byte)BackgroundPixelState.AdaptingLower, (byte)BackgroundPixelState.AdaptingUpper,
        ];

        byte[] inputBackgroundMin =
        [ // R    G    B    A
              24,  24,  24, 255,    0,   0,   0,   0,
             224, 128, 192, 255,    128, 192, 224, 255,
             193, 193, 193, 255,    193, 193, 193, 255
        ];

        byte[] inputBackgroundMax =
        [ // R    G    B    A
             177, 177, 177, 255,    0,   0,   0,   0,
             2,   2,   2,   255,    245, 245, 245, 255,
             193, 193, 193, 255,    193, 193, 193, 255
        ];

        byte[] outColorResponse32bpp = new byte[width * height * 4];

        byte[] expectedColorResponse32bpp =
        [ // AcceptedMinY  AcceptedMaxY  0  A       AcceptedMinY  AcceptedMaxY  0  A
             176 + 24,     255,          0, 255,    01,            01,           01, 01, // 0x01010101
             128,          255,          0, 255,    255,           255,          0,  255,
             192 + 24,     255,          0, 255,    255,           255,          0,  255
        ];

        BallColorThresholding.ComputeBallColorThresholds(width, height,
            inputBackgroundState, inputBackgroundMin, inputBackgroundMax, outColorResponse32bpp, 0x01010101, BallColor.White);

        Assert.True(outColorResponse32bpp.SequenceEqual(expectedColorResponse32bpp));
    }

    [Fact]
    public void Compute_Ball_Color_Thresholds_White_Uses_Darker_Range_Only_When_Background_Allows_It()
    {
        int width = 3;
        int height = 1;

        byte[] inputBackgroundState =
        [
            (byte)BackgroundPixelState.Ok,
            (byte)BackgroundPixelState.Ok,
            (byte)BackgroundPixelState.AdaptingLower,
        ];

        byte[] inputBackgroundMin =
        [ // R    G    B    A
             224, 224, 224, 255,
             210, 210, 210, 255,
             224, 224, 224, 255,
        ];

        byte[] inputBackgroundMax =
        [ // R    G    B    A
             230, 230, 230, 255,
             230, 230, 230, 255,
             230, 230, 230, 255,
        ];

        byte[] outColorResponse32bpp = new byte[width * height * 4];

        byte[] expectedColorResponse32bpp =
        [ // AcceptedMinY  AcceptedMaxY  0  A
             192,          223 - 24,     0, 255,
             229 + 24,     255,          0, 255,
             255,          0,            0, 255,
        ];

        BallColorThresholding.ComputeBallColorThresholds(width, height,
            inputBackgroundState, inputBackgroundMin, inputBackgroundMax, outColorResponse32bpp, 0x01010101, BallColor.White);

        Assert.True(outColorResponse32bpp.SequenceEqual(expectedColorResponse32bpp));
    }
}
