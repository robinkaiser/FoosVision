// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.BlackObjects;
using FoosVision.Vision.ValidationTests.Utils;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using FoosRectangle = FoosVision.Common.Types.Rectangle;

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class BlackObjectMaskOverlayWriter
{
    private const byte _OverlayR = byte.MaxValue;
    private const byte _OverlayG = 220;
    private const byte _OverlayB = 0;
    private const int _OverlayAlpha = 128;

    public static void Write(
        Rgba8888ImageData imageData,
        BlackRodObjectMaskDetection detection,
        string outputPath)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(imageData.Buffer, imageData.Width, imageData.Height);

        image.ProcessPixelRows(accessor =>
        {
            foreach (var rod in detection.Rods)
            {
                foreach (var rectangle in rod.Rectangles)
                {
                    DrawRectangle(accessor, imageData.Width, imageData.Height, rectangle);
                }
            }
        });

        image.SaveAsPng(outputPath);
    }

    private static void DrawRectangle(
        PixelAccessor<Rgba32> accessor,
        int width,
        int height,
        FoosRectangle rectangle)
    {
        int x0 = Math.Clamp(rectangle.X, 0, width);
        int y0 = Math.Clamp(rectangle.Y, 0, height);
        int x1 = Math.Clamp(rectangle.RightExclusive, 0, width);
        int y1 = Math.Clamp(rectangle.BottomExclusive, 0, height);

        for (int y = y0; y < y1; y++)
        {
            Span<Rgba32> row = accessor.GetRowSpan(y);

            for (int x = x0; x < x1; x++)
            {
                Rgba32 source = row[x];

                row[x] = new(
                    Blend(source.R, _OverlayR),
                    Blend(source.G, _OverlayG),
                    Blend(source.B, _OverlayB),
                    byte.MaxValue);
            }
        }
    }

    private static byte Blend(byte source, byte overlay)
    {
        int value = (source * (byte.MaxValue - _OverlayAlpha)) + (overlay * _OverlayAlpha);

        return Convert.ToByte(value / byte.MaxValue);
    }
}
