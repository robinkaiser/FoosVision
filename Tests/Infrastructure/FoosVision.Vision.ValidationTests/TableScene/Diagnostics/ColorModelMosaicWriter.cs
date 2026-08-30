// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Vision.TableScene.Processing.ColoredPlayers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace FoosVision.Vision.ValidationTests.TableScene.Diagnostics;

public static class ColorModelMosaicWriter
{
    private const int _PanelSize = 256;
    private const int _Luma = 160;

    public static void Write(ColoredPlayerColorCalibration calibration, string outputPath)
    {
        using Image<Rgba32> image = new(_PanelSize * 2, _PanelSize);

        image.ProcessPixelRows(accessor =>
        {
            DrawPanel(accessor, 0, calibration.TeamA.ColorModel);
            DrawPanel(accessor, _PanelSize, calibration.TeamB.ColorModel);
        });

        image.SaveAsPng(outputPath);
    }

    private static void DrawPanel(PixelAccessor<Rgba32> accessor, int xOffset, ChromaticColorModel? model)
    {
        for (int cr = 0; cr < _PanelSize; cr++)
        {
            Span<Rgba32> row = accessor.GetRowSpan(cr);

            for (int cb = 0; cb < _PanelSize; cb++)
            {
                Rgba32 color = model is null
                    ? new Rgba32(24, 24, 24, byte.MaxValue)
                    : CreateColor(_Luma, cb, cr);

                if (model is not null &&
                    IsModelMarker(cb, cr, model))
                {
                    color = new Rgba32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
                }

                row[xOffset + cb] = color;
            }
        }

        if (model is null)
        {
            return;
        }

        DrawCenterMarker(accessor, xOffset, model);
    }

    private static bool IsModelMarker(int cb, int cr, ChromaticColorModel model)
    {
        int dCb = cb - model.CenterCb;
        int dCr = cr - model.CenterCr;
        double distance = Math.Sqrt((dCb * dCb) + (dCr * dCr));

        return Math.Abs(distance - model.Radius) < 1.5;
    }

    private static void DrawCenterMarker(PixelAccessor<Rgba32> accessor, int xOffset, ChromaticColorModel model)
    {
        Rgba32 marker = new(0, 0, 0, byte.MaxValue);

        for (int offset = -6; offset <= 6; offset++)
        {
            int x = model.CenterCb + offset;
            int y = model.CenterCr;

            if (x >= 0 && x < _PanelSize && y >= 0 && y < _PanelSize)
            {
                accessor.GetRowSpan(y)[xOffset + x] = marker;
            }

            x = model.CenterCb;
            y = model.CenterCr + offset;

            if (x >= 0 && x < _PanelSize && y >= 0 && y < _PanelSize)
            {
                accessor.GetRowSpan(y)[xOffset + x] = marker;
            }
        }
    }

    private static Rgba32 CreateColor(int y, int cb, int cr)
    {
        double cbDelta = cb - 128;
        double crDelta = cr - 128;
        int r = Convert.ToInt32(Math.Round(y + (1.402 * crDelta)));
        int g = Convert.ToInt32(Math.Round(y - (0.344136 * cbDelta) - (0.714136 * crDelta)));
        int b = Convert.ToInt32(Math.Round(y + (1.772 * cbDelta)));

        return new(
            Convert.ToByte(Math.Clamp(r, 0, byte.MaxValue)),
            Convert.ToByte(Math.Clamp(g, 0, byte.MaxValue)),
            Convert.ToByte(Math.Clamp(b, 0, byte.MaxValue)),
            byte.MaxValue);
    }
}
