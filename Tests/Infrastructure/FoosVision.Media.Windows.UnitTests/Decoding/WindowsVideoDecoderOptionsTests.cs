// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Windows.UnitTests.Decoding;

public class WindowsVideoDecoderOptionsTests
{
    [Fact]
    public void Validate_accepts_supported_configuration()
    {
        WindowsVideoDecoderOptions options = new(CodecType.H264, 1920, 1080);

        options.Validate();
    }

    [Fact]
    public void Validate_rejects_unknown_codec()
    {
        WindowsVideoDecoderOptions options = new(CodecType.Unknown, 1920, 1080);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_rejects_non_positive_dimensions()
    {
        WindowsVideoDecoderOptions options = new(CodecType.H264, 0, 1080);

        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }

    [Fact]
    public void Validate_rejects_unsupported_output_format()
    {
        WindowsVideoDecoderOptions options = new(CodecType.H264, 1920, 1080, (FrameByteFormat)999);

        Assert.Throws<NotSupportedException>(() => options.Validate());
    }
}
