// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Runtime.CompilerServices;

namespace FoosVision.Vision.TableScene.Processing.ColoredPlayers;

public readonly record struct ColorFeature(int Cb, int Cr)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ColorFeature FromRgb(byte r, byte g, byte b)
    {
        int cb = 128 + (((-43 * r) - (85 * g) + (128 * b)) >> 8);
        int cr = 128 + (((128 * r) - (107 * g) - (21 * b)) >> 8);

        return new(
            cb,
            cr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetSquaredDistance(ColorFeature a, ColorFeature b)
    {
        int dCb = a.Cb - b.Cb;
        int dCr = a.Cr - b.Cr;

        return (dCb * dCb) + (dCr * dCr);
    }
}
