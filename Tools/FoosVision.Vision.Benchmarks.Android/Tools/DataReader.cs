// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.Benchmarks.Android.Tools;

public class DataReader
{
    public static byte[] ReadRGBDataFromAssetsIntoRGBABuffer(string fileName, int width, int height)
    {
        var assetManager = Application.Context.Assets ?? throw new Exception($"ReadRGBDataFromAssetsIntoRGBABuffer - No assets");

        using var stream = assetManager.Open(fileName);
        using MemoryStream ms = new();
        stream.CopyTo(ms);

        byte[] rgbAsset = ms.ToArray();
        byte[] rgbaBuffer = new byte[width * height * 4];

        for (int i = 0, j = 0; i < rgbAsset.Length; i += 3, j += 4)
        {
            rgbaBuffer[j + 0] = rgbAsset[i + 0];
            rgbaBuffer[j + 1] = rgbAsset[i + 1];
            rgbaBuffer[j + 2] = rgbAsset[i + 2];
            rgbaBuffer[j + 3] = 255;
        }

        return rgbaBuffer;
    }
}
