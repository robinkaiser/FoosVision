// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Media;

public enum YuvPlaneKind
{
    Y,
    U,
    V,
}

public readonly record struct YuvPlaneLayout(
    YuvPlaneKind Kind,
    int Width,
    int Height,
    int RowStride,
    int PixelStride)
{
    public int BufferLength => RowStride * Height;
}

public readonly record struct YuvFrameLayout(
    int Width,
    int Height,
    YuvPlaneLayout Y,
    YuvPlaneLayout U,
    YuvPlaneLayout V);
