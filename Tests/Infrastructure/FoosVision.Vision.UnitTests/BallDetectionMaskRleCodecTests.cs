// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.UnitTests;

public class BallDetectionMaskRleCodecTests
{
    private const int _Width = 4;
    private const int _Height = 4;

    private readonly byte[] _Gray8Image =
    [
        8, 4, 0, 2,
        0, 6, 0, 0,
        0, 0, 254, 0,
        0, 0, 0, 0,
    ];

    private readonly byte[] _RleBuffer =
    [
        4, 2, 128 + 1, 1,
        128 + 1, 3, 128 + 4,
        127,
        128 + 5,
    ];

    [Fact]
    public void Encode_uses_zero_runs_and_half_intensity_non_zero_values()
    {
        byte[] output = new byte[BallDetectionMaskRleCodec.GetMaxEncodedLength(_Width * _Height)];

        int length = BallDetectionMaskRleCodec.Encode(_Width, _Height, _Gray8Image, output);

        Assert.Equal(_RleBuffer.Length, length);
        Assert.True(output.AsSpan(0, length).SequenceEqual(_RleBuffer));
    }

    [Fact]
    public void Decode_restores_gray8_values()
    {
        byte[] output = new byte[_Width * _Height];

        BallDetectionMaskRleCodec.DecodeToGray8(_Width, _Height, _RleBuffer, _RleBuffer.Length, output);

        Assert.True(output.SequenceEqual(_Gray8Image));
    }

    [Fact]
    public void Encode_splits_long_zero_runs()
    {
        byte[] input = new byte[8 * 16];
        byte[] output = new byte[BallDetectionMaskRleCodec.GetMaxEncodedLength(input.Length)];

        int length = BallDetectionMaskRleCodec.Encode(8, 16, input, output);

        Assert.Equal(2, length);
        Assert.Equal(128 + 127, output[0]);
        Assert.Equal(128 + 1, output[1]);
    }
}
