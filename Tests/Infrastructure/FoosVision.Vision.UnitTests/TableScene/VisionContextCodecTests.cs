// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.Common;
using FoosVision.Vision.TableScene.Processing;

namespace FoosVision.Vision.UnitTests.TableScene;

public class VisionContextCodecTests
{
    [Fact]
    public void Encode_And_Decode_Vision_Context()
    {
        int width = 8;
        int height = 2;
        int pixelCount = width * height;

        byte[] inputColorResponse32bpp =
        [ // R    G    B    A
              0,   0,   0, 255,     0,   0,   0, 255,     0,   0,   0, 255,     0,   0,   0, 255,
            100, 151,   4, 255,   100, 151,   4, 255,   100, 151,   4, 255,   200,  55,   8, 255,
            200,  55,   8, 255,   200,  55,   8, 255,   100, 151,   4, 255,   100, 151,   4, 255,
             17,  29,  41, 255,    17,  29,  41, 255,    17,  29,  41, 255,    17,  29,  41, 255
        ];

        byte[] encoded = new byte[VisionContextCodec.GetMaxEncodedLength(pixelCount)];
        byte[] decodedColorResponse32bpp = new byte[inputColorResponse32bpp.Length];
        int[] valueCounts = new int[VisionContextCodec.QuantizedColorCount];
        int[] paletteValues = new int[VisionContextCodec.QuantizedColorCount];
        ushort[] valueIndices = new ushort[VisionContextCodec.QuantizedColorCount];
        PlayerColorExclusionContext inputPlayerColorExclusion = new(
            true,
            new(117, 160, 100, 625),
            true,
            new(106, 100, 144, 625));

        bool encodedSuccessfully = VisionContextCodec.TryEncode(width, height,
            inputColorResponse32bpp, inputPlayerColorExclusion, encoded, valueCounts, paletteValues, valueIndices, out int encodedLength);
        bool decodedSuccessfully = VisionContextCodec.TryDecode(encoded, encodedLength,
            decodedColorResponse32bpp, paletteValues, out PlayerColorExclusionContext decodedPlayerColorExclusion);

        Assert.True(encodedSuccessfully);
        Assert.True(decodedSuccessfully);
        Assert.True(encodedLength <= encoded.Length);
        Assert.True(decodedColorResponse32bpp.SequenceEqual(GetExpectedColorResponse32bpp(inputColorResponse32bpp)));
        Assert.Equal(inputPlayerColorExclusion, decodedPlayerColorExclusion);
    }

    private static byte[] GetExpectedColorResponse32bpp(byte[] inputColorResponse32bpp)
    {
        byte[] expectedColorResponse32bpp = new byte[inputColorResponse32bpp.Length];

        for (int i = 0; i < inputColorResponse32bpp.Length; i += 4)
        {
            expectedColorResponse32bpp[i] = QuantizeAndExpand(inputColorResponse32bpp[i]);
            expectedColorResponse32bpp[i + 1] = QuantizeAndExpand(inputColorResponse32bpp[i + 1]);
            expectedColorResponse32bpp[i + 2] = QuantizeAndExpand(inputColorResponse32bpp[i + 2]);
            expectedColorResponse32bpp[i + 3] = 255;
        }

        return expectedColorResponse32bpp;
    }

    private static byte QuantizeAndExpand(byte value)
    {
        int quantized = value >> 2;

        return (byte)((quantized << 2) | (quantized >> 4));
    }
}
