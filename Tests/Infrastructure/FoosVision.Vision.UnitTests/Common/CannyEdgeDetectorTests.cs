// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Vision.Common.Processing;

namespace FoosVision.Vision.UnitTests.Common;

public class CannyEdgeDetectorTests
{
#pragma warning disable IDE1006 // Naming Styles
    private const byte W = 1;
#pragma warning restore IDE1006 // Naming Styles

    [Fact]
    public void Mono_Black()
    {
        byte[] input =
        [
            0,  0,  0,  0,
            0,  0,  0,  0,
            0,  0,  0,  0,
            0,  0,  0,  0,
        ];

        Test(input, input, 4, 4);
    }

    [Fact]
    public void Mono_White()
    {
        byte[] input =
        [
            0,  0,  0,  0,  0,  0,
            0,  W,  W,  W,  W,  0,
            0,  W,  W,  W,  W,  0,
            0,  W,  W,  W,  W,  0,
            0,  W,  W,  W,  W,  0,
            0,  0,  0,  0,  0,  0,
        ];

        byte[] expectedOutput =
        [
            0,   0,   0,   0,   0,  0,
            0, 255, 240, 240, 255,  0,
            0, 240,   0,   0, 240,  0,
            0, 240,   0,   0, 240,  0,
            0, 255, 240, 240, 255,  0,
            0,  0,   0,   0,   0,  0,
        ];

        Test(input, expectedOutput, 6, 6);
    }

    [Fact]
    public void Region()
    {
        byte[] input =
        [
            0,  0,  0,  0,  0,  0,
            0,  W,  0,  W,  W,  0,
            0,  W,  0,  0,  0,  0,
            0,  W,  0,  W,  W,  0,
            0,  W,  0,  W,  W,  0,
            0,  0,  0,  0,  0,  0,
        ];

        byte[] expectedOutput =
        [
            0,  0,  0,   0,   0,  0,
            0,  0,  0,   0,   0,  0,
            0,  0,  0,   0,   0,  0,
            0,  0,  0, 255, 255,  0,
            0,  0,  0, 255, 255,  0,
            0,  0,  0,   0,   0,  0,
        ];

        Test(input, expectedOutput, 6, 6, new Rectangle(2, 2, 4, 4));
    }

    [Fact]
    public void Points()
    {
        byte[] input =
        [
            0,  0,  0,  0,  0,  0,
            0,  W,  W,  0,  0,  0,
            0,  W,  W,  0,  0,  0,
            0,  0,  0,  0,  0,  0,
            0,  W,  W,  0,  W,  0,
            0,  0,  0,  0,  0,  0,
        ];

        EdgePoint[] expectedOutput =
        [
            new(1, 1),
            new(2, 1),
            new(1, 2),
            new(2, 2),
        ];

        Test(input, expectedOutput, 6, 6, new Rectangle(0, 0, 6, 4));
    }

    private static void Test(byte[] input, byte[] expectedOutput, int width, int height, Rectangle? roi = null)
    {
        CannyEdgeDetector testee = new(width, height);
        roi ??= new Rectangle(0, 0, width, height);

        byte[] output = new byte[width * height];
        testee.Process(input, output, (Rectangle)roi);

        Assert.True(output.SequenceEqual(expectedOutput));
    }

    private static void Test(byte[] input, EdgePoint[] expectedOutput, int width, int height, Rectangle? roi = null)
    {
        CannyEdgeDetector testee = new(width, height);
        roi ??= new Rectangle(0, 0, width, height);

        EdgePoint[] points = new EdgePoint[width * height];
        int pointCount = testee.Process(input, (Rectangle)roi, points);

        Assert.True(points.Take(pointCount).SequenceEqual(expectedOutput));
    }
}
