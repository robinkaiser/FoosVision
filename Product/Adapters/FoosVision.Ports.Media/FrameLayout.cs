// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Media;

public enum FrameByteFormat
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

public readonly record struct FrameLayout(
    FrameByteFormat Format,
    int Width,
    int Height,
    int Stride);
