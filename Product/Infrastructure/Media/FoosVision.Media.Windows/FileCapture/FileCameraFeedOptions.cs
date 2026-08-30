// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Windows.FileCapture;

public record FileCameraFeedOptions(
    string FilePath,
    CodecType Codec,
    int Width,
    int Height,
    int EncodedFps = 120,
    int DecodedFps = 30,
    FrameByteFormat OutputFormat = FrameByteFormat.RGBA8888,
    WindowsVideoDecoderHardwareMode HardwareMode = WindowsVideoDecoderHardwareMode.PreferHardware)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("File path must be specified.", nameof(FilePath));
        }

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

        if (EncodedFps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(EncodedFps), "EncodedFps must be greater than zero.");
        }

        if (DecodedFps <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(DecodedFps), "DecodedFps must be greater than zero.");
        }

        if (EncodedFps % DecodedFps != 0)
        {
            throw new ArgumentException("DecodedFps must divide EncodedFps for deterministic playback sampling.", nameof(DecodedFps));
        }

        if (OutputFormat != FrameByteFormat.RGBA8888)
        {
            throw new NotSupportedException($"Output format '{OutputFormat}' is not supported.");
        }
    }
}
