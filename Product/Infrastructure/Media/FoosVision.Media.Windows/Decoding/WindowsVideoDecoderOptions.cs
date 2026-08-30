// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Windows.Decoding;

public record WindowsVideoDecoderOptions(
    CodecType Codec,
    int Width,
    int Height,
    FrameByteFormat OutputFormat = FrameByteFormat.RGBA8888,
    WindowsVideoDecoderHardwareMode HardwareMode = WindowsVideoDecoderHardwareMode.PreferHardware)
{
    public void Validate()
    {
        if (Codec == CodecType.Unknown)
        {
            throw new ArgumentException("Codec must be specified.", nameof(Codec));
        }

        if (Width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Width must be greater than zero.");
        }

        if (Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Height), "Height must be greater than zero.");
        }

        if (OutputFormat != FrameByteFormat.RGBA8888)
        {
            throw new NotSupportedException($"Output format '{OutputFormat}' is not supported.");
        }
    }
}
