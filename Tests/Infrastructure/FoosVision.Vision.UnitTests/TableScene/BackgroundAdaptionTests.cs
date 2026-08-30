// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene;
using FoosVision.Vision.TableScene.Processing;

namespace FoosVision.Vision.UnitTests.TableScene;

public class BackgroundAdaptionTests
{
#pragma warning disable IDE1006
    private const byte Ignored = (byte)BackgroundPixelState.IgnoredPixel;
    private const byte Ok = (byte)BackgroundPixelState.Ok;
    private const byte AdaptingUpper = (byte)BackgroundPixelState.AdaptingUpper;
    private const byte AdaptingLower = (byte)BackgroundPixelState.AdaptingLower;
    private const byte NotInitialized = (byte)BackgroundPixelState.NotInitialized;
#pragma warning restore IDE1006

    [Fact]
    public void Reset_Ignored_Pixels()
    {
        int width = 2;
        int height = 2;

        byte[] inOutBackgroundState =
        [
            Ok,
            Ignored,
            Ok,
            Ignored,
        ];

        byte[] inOutBackgroundMin =
        [ // R   G   B   A
             42, 42, 42, 255,
             42, 42, 42, 255,
             42, 42, 42, 255,
             42, 42, 42, 255,
        ];

        byte[] inOutBackgroundMax =
        [ // R   G   B   A
             42, 42, 42, 255,
             42, 42, 42, 255,
             42, 42, 42, 255,
             42, 42, 42, 255,
        ];

        byte[] expectedBackgroundState =
        [
            Ok,
            NotInitialized,
            Ok,
            NotInitialized,
        ];

        byte[] expectedBackground =
        [ // R   G   B   A
             42, 42, 42, 255,
             0,  0,  0,  0,
             42, 42, 42, 255,
             0,  0,  0,  0,
        ];

        BackgroundAdaption.ResetIgnoredPixels(width, height, inOutBackgroundState, inOutBackgroundMin, inOutBackgroundMax);

        Assert.True(inOutBackgroundState.SequenceEqual(expectedBackgroundState));
        Assert.True(inOutBackgroundMin.SequenceEqual(expectedBackground));
        Assert.True(inOutBackgroundMax.SequenceEqual(expectedBackground));
    }

    [Fact]
    public void Update_Model_From_Rgba()
    {
        int width = 1;
        int height = 9;

        // Ignored Rgba pixel
        // Ignored Bg state pixel (outside playing field)
        // Not initialized

        // Increase: small step
        // Increase: big jump
        // Increase: below threshold

        // Decrease: small step
        // Decrease: big jump
        // Decrease: below threshold

        byte[] inputRgba =
        [ // R    G    B    A
               0,   0,   0, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,

             131, 100, 100, 255,
             100, 132, 100, 255,
             100, 100, 103, 255,

              69, 100, 100, 255,
             100,  68, 100, 255,
             100, 100,  97, 255,
        ];

        byte[] inOutBackgroundState =
        [
            Ok,
            Ignored,
            NotInitialized,

            AdaptingUpper,
            Ok,
            AdaptingUpper,

            Ok,
            Ok,
            Ok,
        ];

        byte[] inOutBackgroundMin =
        [ // R    G    B    A
             100, 100, 100, 255,
             100, 100, 100, 255,
               0,   0,   0,   0,

             100, 100, 100, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,

             100, 100, 100, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,
        ];

        byte[] inOutBackgroundMax =
        [ // R    G    B    A
             100, 100, 100, 255,
             100, 100, 100, 255,
               0,   0,   0,   0,

             100, 100, 100, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,

             100, 100, 100, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,
        ];

        byte[] expectedBackgroundState =
        [
            Ok,
            Ignored,
            Ok,

            Ok,
            AdaptingUpper,
            Ok,

            Ok,
            AdaptingLower,
            Ok,
        ];

        byte[] expectedBackgroundMin =
        [ // R    G    B    A
             100, 100, 100, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,

             100, 100, 100, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,

              88, 100, 100, 255,
             100,  88, 100, 255,
             100, 100, 100, 255,
        ];

        byte[] expectedBackgroundMax =
        [    // R    G    B    A
             100, 100, 100, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,

             112, 100, 100, 255,
             100, 112, 100, 255,
             100, 100, 100, 255,

             100, 100, 100, 255,
             100, 100, 100, 255,
             100, 100, 100, 255,
        ];

        BackgroundAdaption.UpdateModelFromRgba(width, height, inputRgba, inOutBackgroundState, inOutBackgroundMin, inOutBackgroundMax);

        Assert.True(inOutBackgroundState.SequenceEqual(expectedBackgroundState));
        Assert.True(inOutBackgroundMin.SequenceEqual(expectedBackgroundMin));
        Assert.True(inOutBackgroundMax.SequenceEqual(expectedBackgroundMax));
    }
}
