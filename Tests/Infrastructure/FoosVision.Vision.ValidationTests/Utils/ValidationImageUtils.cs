// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.InteropServices;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace FoosVision.Vision.ValidationTests.Utils;

public enum ColorType
{
    Red,
    Pink,
    Green,
    Blue,
    White,
    Black,
}

public enum StyleType
{
    Dash,
    Dot,
    Solid,
}

public record Rgba8888ImageData(byte[] Buffer, int Width, int Height);

public record LineData(double X0, double Y0, double X1, double Y1, ColorType Color, StyleType Style, float Thickness = 2.0f);

public static class ValidationImageUtils
{
    public static Rgba8888ImageData ReadRGBA8888ImageFromFile(string inputFilePath)
    {
        using Image<Rgba32> image = Image.Load<Rgba32>(inputFilePath);

        var pixels = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixels);

        Span<byte> asBytes = MemoryMarshal.AsBytes(pixels.AsSpan());

        return new(asBytes.ToArray(), image.Width, image.Height);
    }

    public static void WriteY8ImageToFile(byte[] y8Buffer, int width, int height, string outputFilePath)
    {
        using Image<L8> image = Image.LoadPixelData<L8>(y8Buffer, width, height);

        var encoder = new PngEncoder
        {
            ColorType = PngColorType.Grayscale,
            BitDepth = PngBitDepth.Bit8,
        };

        image.Save(outputFilePath, encoder);
    }

    public static void WriteRGBA8888ImageToFile(byte[] imageBuffer, int width, int height, string outputFilePath)
    {
        using Image<Rgba32> image = Image.LoadPixelData<Rgba32>(imageBuffer, width, height);
        image.SaveAsPng(outputFilePath);
    }

    public static void WriteArgbImageToFile(Rgba8888ImageData imageData, string outputFilePath)
        => WriteRGBA8888ImageToFile(imageData.Buffer, imageData.Width, imageData.Height, outputFilePath);

    public static void WriteRGBA8888ImageWithLinesToFile(Rgba8888ImageData imageData, IEnumerable<LineData> lines, string outputFilePath)
    {
        var image = Image.LoadPixelData<Rgba32>(imageData.Buffer, imageData.Width, imageData.Height);

        image.Mutate(ctx =>
        {
            foreach (var line in lines)
            {
                Color color = GetColor(line.Color);
                float thickness = line.Thickness;
                Pen pen = GetPen(line.Style, color, thickness);

                PointF p0 = new(Convert.ToSingle(line.X0), Convert.ToSingle(line.Y0));
                PointF p1 = new(Convert.ToSingle(line.X1), Convert.ToSingle(line.Y1));
                ctx.DrawLine(pen, p0, p1);
            }
        });

        image.SaveAsPng(outputFilePath);
    }

    private static Color GetColor(ColorType color)
    {
        return color switch
        {
            ColorType.Red => Color.Red,
            ColorType.Pink => Color.Magenta,
            ColorType.Green => Color.Green,
            ColorType.Blue => Color.Blue,
            ColorType.White => Color.White,
            ColorType.Black or _ => Color.Black
        };
    }

    private static Pen GetPen(StyleType style, Color color, float thickness)
    {
        return style switch
        {
            StyleType.Dash => Pens.Dash(color, thickness),
            StyleType.Dot => Pens.Dot(color, thickness),
            StyleType.Solid or _ => Pens.Solid(color, thickness)
        };
    }
}
