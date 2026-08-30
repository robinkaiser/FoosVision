// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Media.Windows.FileCapture;

namespace FoosVision.Media.Windows.UnitTests.FileCapture;

public class FileCameraFeedOptionsTests
{
    [Fact]
    public void Validate_accepts_default_supported_values()
    {
        FileCameraFeedOptions options = new("clip.mp4", CodecType.H264, 1920, 1080);

        options.Validate();
    }

    [Fact]
    public void Validate_rejects_unknown_codec()
    {
        FileCameraFeedOptions options = new("clip.mp4", CodecType.Unknown, 1920, 1080);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_rejects_non_divisible_frame_rates()
    {
        FileCameraFeedOptions options = new("clip.mp4", CodecType.H264, 1920, 1080, EncodedFps: 120, DecodedFps: 50);

        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void Validate_keeps_hardware_mode_default_on_prefer_hardware()
    {
        FileCameraFeedOptions options = new("clip.mp4", CodecType.H265, 1920, 1080);

        Assert.Equal(WindowsVideoDecoderHardwareMode.PreferHardware, options.HardwareMode);
    }
}
