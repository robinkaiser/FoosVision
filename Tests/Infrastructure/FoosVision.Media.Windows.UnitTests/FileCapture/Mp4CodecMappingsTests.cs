// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FFmpeg.AutoGen;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.FileCapture.Mp4;

namespace FoosVision.Media.Windows.UnitTests.FileCapture;

public class Mp4CodecMappingsTests
{
    [Fact]
    public void ResolveCodecType_maps_h264()
    {
        Assert.Equal(CodecType.H264, Mp4CodecMappings.ResolveCodecType(AVCodecID.AV_CODEC_ID_H264));
    }

    [Fact]
    public void ResolveCodecType_maps_h265()
    {
        Assert.Equal(CodecType.H265, Mp4CodecMappings.ResolveCodecType(AVCodecID.AV_CODEC_ID_HEVC));
    }

    [Fact]
    public void ResolveBitstreamFilterName_maps_h264_to_mp4_filter()
    {
        Assert.Equal("h264_mp4toannexb", Mp4CodecMappings.ResolveBitstreamFilterName(CodecType.H264));
    }

    [Fact]
    public void ResolveBitstreamFilterName_rejects_unknown_codec()
    {
        Assert.Throws<NotSupportedException>(() => Mp4CodecMappings.ResolveBitstreamFilterName(CodecType.Unknown));
    }
}
