// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Vision.BallFinding.Processing;

namespace FoosVision.Vision.UnitTests.BallFinding;

public class BlobFinderTests
{
#pragma warning disable IDE1006 // Naming Styles
    private const byte W = 1;
#pragma warning restore IDE1006 // Naming Styles

    [Fact]
    public void Mono_Black()
    {
        byte[] input =
        [
            0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,
            0,  0,  0,  0,  0,
        ];

        Blob[] expectedOutput =
        [];

        Test(input, expectedOutput, 5, 5);
    }

    [Fact]
    public void Mono_White()
    {
        byte[] input =
        [
            W,  W,  W,  W,  W,
            W,  W,  W,  W,  W,
            W,  W,  W,  W,  W,
            W,  W,  W,  W,  W,
            W,  W,  W,  W,  W,
        ];

        Blob[] expectedOutput =
        [
            new(25, 0, 0, 4, 4)
        ];

        Test(input, expectedOutput, 5, 5);
    }

    [Fact]
    public void Corner_Upper_Left()
    {
        byte[] input =
        [
            W,  0,
            0,  0,
        ];

        Blob[] expectedOutput =
        [
            new(1, 0, 0, 0, 0)
        ];

        Test(input, expectedOutput, 2, 2);
    }

    [Fact]
    public void Corner_Lower_Left()
    {
        byte[] input =
        [
            0,  0,
            W,  0,
        ];

        Blob[] expectedOutput =
        [
            new(1, 0, 1, 0, 1)
        ];

        Test(input, expectedOutput, 2, 2);
    }

    [Fact]
    public void Corner_Upper_Right()
    {
        byte[] input =
        [
            0,  W,
            0,  0,
        ];

        Blob[] expectedOutput =
        [
            new(1, 1, 0, 1, 0)
        ];

        Test(input, expectedOutput, 2, 2);
    }

    [Fact]
    public void Corner_Lower_Right()
    {
        byte[] input =
        [
            0,  0,
            0,  W,
        ];

        Blob[] expectedOutput =
        [
            new(1, 1, 1, 1, 1)
        ];

        Test(input, expectedOutput, 2, 2);
    }

    [Fact]
    public void Diagonal1()
    {
        byte[] input =
        [
            W,  0,  0,
            0,  W,  0,
            0,  0,  W,
        ];

        Blob[] expectedOutput =
        [
            new(3, 0, 0, 2, 2),
        ];

        Test(input, expectedOutput, 3, 3);
    }

    [Fact]
    public void Diagonal2()
    {
        byte[] input =
        [
            0,  0,  W,
            0,  W,  0,
            W,  0,  0,
        ];

        Blob[] expectedOutput =
        [
            new(3, 0, 0, 2, 2),
        ];

        Test(input, expectedOutput, 3, 3);
    }

    [Fact]
    public void Plateau()
    {
        byte[] input =
        [
            0,  W,  W,
            W,  0,  W,
            W,  W,  0,
        ];

        Blob[] expectedOutput =
        [
            new(6, 0, 0, 2, 2)
        ];

        Test(input, expectedOutput, 3, 3);
    }

    [Fact]
    public void Touching_Boundary_EndEqualsPrevStart_Connects_Selectively()
    {
        byte[] input =
        [
            0,  0,  W,  0,  W,  0,
            0,  W,  0,  0,  0,  0,
        ];

        Blob[] expectedOutput =
        [   // Expected finalization order: the right isolated pixel (4,0) finalizes earlier than the 2-pixel component.
            new(1, 4, 0, 4, 0),
            new(2, 1, 0, 2, 1),
        ];

        Test(input, expectedOutput, 6, 2);
    }

    [Fact]
    public void Checkerboard()
    {
        byte[] input =
        [
            W,  0,  W,  0,  W,  0,  W,  0,
            0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,
            0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,
            0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,
            0,  W,  0,  W,  0,  W,  0,  W,
        ];

        Blob[] expectedOutput =
        [
            new(32, 0, 0, 7, 7),
        ];

        Test(input, expectedOutput, 8, 8);
    }

    [Fact]
    public void Spiral()
    {
        byte[] input =
        [
            W,  W,  W,  W,  W,  W,  0,  W,
            W,  0,  0,  0,  0,  W,  0,  W,
            W,  0,  W,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  0,  W,  0,  W,
            W,  0,  W,  0,  0,  W,  0,  W,
            W,  0,  W,  W,  W,  W,  0,  W,
            W,  0,  0,  0,  0,  0,  0,  W,
            W,  W,  W,  W,  W,  W,  W,  W,
        ];

        Blob[] expectedOutput =
        [
            new(39, 0, 0, 7, 7)
        ];

        Test(input, expectedOutput, 8, 8);
    }

    [Fact]
    public void Meander()
    {
        byte[] input =
        [
            W,  0,  W,  W,  W,  0,  W,  W,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  W,  W,  0,  W,  W,  W,  0,  W,
        ];

        Blob[] expectedOutput =
        [
            new(24, 0, 0, 8, 3)
        ];

        Test(input, expectedOutput, 9, 4);
    }

    [Fact]
    public void Comb_Bottom_Connect()
    {
        byte[] input =
        [
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  W,  W,  W,  W,  W,  W,  W,  W,
        ];

        Blob[] expectedOutput =
        [
            new(34, 0, 0, 8, 5),
        ];

        Test(input, expectedOutput, 9, 6);
    }

    [Fact]
    public void Comb_Top_Connect()
    {
        byte[] input =
        [
            W,  W,  W,  W,  W,  W,  W,  W,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,  0,  W,
        ];

        Blob[] expectedOutput =
        [
            new(34, 0, 0, 8, 5),
        ];

        Test(input, expectedOutput, 9, 6);
    }

    [Fact]
    public void Blob_Inside_Blob()
    {
        byte[] input =
        [
            W,  W,  W,  W,  W,  W,  W,
            W,  0,  0,  0,  0,  0,  W,
            W,  0,  W,  W,  W,  0,  W,
            W,  0,  W,  0,  W,  0,  W,
            W,  0,  W,  W,  W,  0,  W,
            W,  0,  0,  0,  0,  0,  W,
            W,  W,  W,  W,  W,  W,  W,
        ];

        Blob[] expectedOutput =
        [
            new(8, 2, 2, 4, 4),
            new(24, 0, 0, 6, 6),
        ];

        Test(input, expectedOutput, 7, 7);
    }

    [Fact]
    public void Min_Size()
    {
        byte[] input =
        [
            W,  0,  W,  0,  W,  0,  W,
            0,  0,  W,  0,  0,  W,  0,
        ];

        Blob[] expectedOutput =
        [
            new(3, 4, 0, 6, 1),
        ];

        Test(input, expectedOutput, 7, 2,
            3);
    }

    [Fact]
    public void Min_Extend_X()
    {
        byte[] input =
        [
            W,  W,  0,  W,  0,
            0,  0,  0,  W,  0,
            W,  W,  W,  W,  0,
            0,  0,  0,  0,  0,
            W,  W,  W,  W,  W,
        ];

        Blob[] expectedOutput =
        [
            new(5, 0, 4, 4, 4),
        ];

        Test(input, expectedOutput, 5, 5,
            1, 5, 1);
    }

    [Fact]
    public void Min_Extend_Y()
    {
        byte[] input =
        [
            W,  0,  W,  0,  W,
            W,  0,  W,  0,  W,
            0,  0,  W,  0,  W,
            W,  W,  W,  0,  W,
            0,  0,  0,  0,  W,
        ];

        Blob[] expectedOutput =
        [
            new(5, 4, 0, 4, 4),
        ];

        Test(input, expectedOutput, 5, 5,
            1, 1, 5);
    }

    [Fact]
    public void Max_Blob_Count()
    {
        byte[] input =
        [
            W,  0,  W,  0,  W,  0,  W,
        ];

        Blob[] expectedOutput =
        [
            new(1, 0, 0, 0, 0),
            new(1, 2, 0, 2, 0),
        ];

        Test(input, expectedOutput, 7, 1,
            1, 1, 1, 2);
    }

    [Fact]
    public void Region_Of_Interest()
    {
        byte[] input =
        [ //        *   *
            W,  0,  0,  W,  0,
            W,  0,  W,  W,  W, // *
            0,  0,  0,  0,  0, // *
            W,  W,  0,  W,  0, // *
            0,  0,  0,  W,  0,
        ];

        Blob[] expectedOutput =
        [
             new(2, 2, 1, 3, 1),
             new(1, 3, 3, 3, 3)
        ];

        Test(input, expectedOutput, 5, 5,
            1, 1, 1, 10, new Rectangle(2, 1, 2, 3));
    }

    [Fact]
    public void Iterations()
    {
        byte[] input =
        [
            0,  W,  0,
            0,  W,  0,
            W,  0,  W,
        ];

        Blob[] expectedOutput =
        [
            new(4, 0, 0, 2, 2)
        ];

        BlobFinderParameters param = new()
        {
            MinBlobSize = 1,
            MinExtendX = 1,
            MinExtendY = 1,
            MaxBlobCount = 42,
        };

        BlobFinder testee = new(3, param);

        for (int i = 0; i < 100; i++)
        {
            var fullRoi = new Rectangle(0, 0, 3, 3);
            int blobCount = testee.ProcessY8(input, fullRoi);

            Assert.True(testee.ResultBlobBuffer.Take(blobCount).SequenceEqual(expectedOutput));
            Assert.Equal(SelfCheckStatus.OkPoolFullyFree, testee.SelfCheck());
        }
    }

    [Fact]
    public void Max_Blobs()
    {
        int width = 128;
        int height = 64;

        var input = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if ((y % 2) == 0)
                {
                    input[(y * width) + x] = (byte)(((x % 2) == 0) ? 1 : 0);
                }
            }
        }

        BlobFinderParameters param = new()
        {
            MinBlobSize = 1,
            MinExtendX = 1,
            MinExtendY = 1,
            MaxBlobCount = width * height,
        };

        BlobFinder testee = new(width, param);

        var fullRoi = new Rectangle(0, 0, width, height);
        int blobCount = testee.ProcessY8(input, fullRoi);

        int expectedBlobCount = (int)(width * height / 4.0);
        Assert.Equal(expectedBlobCount, blobCount);
        Assert.Equal(SelfCheckStatus.OkPoolFullyFree, testee.SelfCheck());
    }

    [Fact]
    public void Max_Merge()
    {
        int width = 64;
        int height = 128;

        var input = new byte[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                input[(y * width) + x] = (byte)(((x % 2) == 0) ? 1 : 0);
            }
        }

        BlobFinderParameters param = new()
        {
            MinBlobSize = 1,
            MinExtendX = 1,
            MinExtendY = 1,
            MaxBlobCount = width * height,
        };

        BlobFinder testee = new(width, param);

        var fullRoi = new Rectangle(0, 0, width, height);
        int blobCount = testee.ProcessY8(input, fullRoi);

        int expectedBlobCount = (int)(width / 2.0);
        Assert.Equal(expectedBlobCount, blobCount);
        Assert.Equal(SelfCheckStatus.OkPoolFullyFree, testee.SelfCheck());
    }

    [Fact]
    public void Random()
    {
        var random = new Random(42);
        int width = 128;
        int height = 128;

        var input = new byte[width * height];

        for (int i = 0; i < 10; i++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    input[(y * width) + x] = (byte)random.Next(0, 2);
                }
            }

            BlobFinderParameters param = new()
            {
                MinBlobSize = 1,
                MinExtendX = 1,
                MinExtendY = 1,
                MaxBlobCount = width * height,
            };

            BlobFinder testee = new(width, param);

            var fullRoi = new Rectangle(0, 0, width, height);
            _ = testee.ProcessY8(input, fullRoi);

            Assert.Equal(SelfCheckStatus.OkPoolFullyFree, testee.SelfCheck());
        }
    }

    private static void Test(byte[] input, IEnumerable<Blob> expectedOutput,
        int width, int height, int minSize = 1, int minExtendX = 1, int minExtendY = 1,
        int maxBlobCount = 100, Rectangle? roi = null)
    {
        BlobFinderParameters param = new()
        {
            MinBlobSize = minSize,
            MinExtendX = minExtendX,
            MinExtendY = minExtendY,
            MaxBlobCount = maxBlobCount,
        };

        BlobFinder testee = new(width, param);
        roi ??= new Rectangle(0, 0, width, height);

        int blobCount = testee.ProcessY8(input, roi.Value);

        Assert.True(testee.ResultBlobBuffer.Take(blobCount).SequenceEqual(expectedOutput));
        Assert.Equal(SelfCheckStatus.OkPoolFullyFree, testee.SelfCheck());
    }
}
