// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision;

public enum VisionPixelFormat
{
    /// <summary>
    /// ABGR32 (Little endian)
    /// bits 31..24 : A
    /// bits 23..16 : B
    /// bits 15..8  : G
    /// bits 7..0   : R
    /// </summary>
    RGBA8888,
}

public readonly record struct VisionFrameLayout(
    VisionPixelFormat Format,
    int Width,
    int Height,
    int Stride);

public record struct VisionOptions(VisionFrameLayout Layout);
