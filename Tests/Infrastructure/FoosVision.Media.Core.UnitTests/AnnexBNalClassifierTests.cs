// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Core.EncodedVideo.AnnexB;

namespace FoosVision.Media.Core.UnitTests;

public class AnnexBNalClassifierTests
{
    [Fact]
    public void DetectCodecFromHeaderByte_returns_unknown_for_non_parameter_set_byte()
    {
        byte headerByte = 0x01;

        CodecType codec = AnnexBNalClassifier.DetectCodecFromHeaderByte(headerByte);

        Assert.Equal(CodecType.Unknown, codec);
    }

    [Fact]
    public void DetectCodecFromHeaderByte_detects_h264_from_sps()
    {
        byte headerByte = 0x67;

        CodecType codec = AnnexBNalClassifier.DetectCodecFromHeaderByte(headerByte);

        Assert.Equal(CodecType.H264, codec);
    }

    [Fact]
    public void DetectCodecFromHeaderByte_detects_h264_from_pps()
    {
        byte headerByte = 0x68;

        CodecType codec = AnnexBNalClassifier.DetectCodecFromHeaderByte(headerByte);

        Assert.Equal(CodecType.H264, codec);
    }

    [Fact]
    public void DetectCodecFromHeaderByte_detects_h265_from_vps()
    {
        byte headerByte = 0x40;

        CodecType codec = AnnexBNalClassifier.DetectCodecFromHeaderByte(headerByte);

        Assert.Equal(CodecType.H265, codec);
    }

    [Fact]
    public void DetectCodecFromHeaderByte_detects_h265_from_sps()
    {
        byte headerByte = 0x42;

        CodecType codec = AnnexBNalClassifier.DetectCodecFromHeaderByte(headerByte);

        Assert.Equal(CodecType.H265, codec);
    }

    [Fact]
    public void GetNalUnitType_returns_h264_unit_type()
    {
        byte headerByte = 0x65;

        int nalUnitType = AnnexBNalClassifier.GetNalUnitType(CodecType.H264, headerByte);

        Assert.Equal(5, nalUnitType);
    }

    [Fact]
    public void GetNalUnitType_returns_h265_unit_type()
    {
        byte headerByte = 0x26;

        int nalUnitType = AnnexBNalClassifier.GetNalUnitType(CodecType.H265, headerByte);

        Assert.Equal(19, nalUnitType);
    }

    [Fact]
    public void GetParameterSetType_returns_sps_for_h264_sps()
    {
        ParameterSetType parameterSetType = AnnexBNalClassifier.GetParameterSetType(CodecType.H264, 7);

        Assert.Equal(ParameterSetType.SPS, parameterSetType);
    }

    [Fact]
    public void GetParameterSetType_returns_vps_for_h265_vps()
    {
        ParameterSetType parameterSetType = AnnexBNalClassifier.GetParameterSetType(CodecType.H265, 32);

        Assert.Equal(ParameterSetType.VPS, parameterSetType);
    }

    [Fact]
    public void IsVclNalUnit_returns_true_for_h264_idr()
    {
        bool isVclNalUnit = AnnexBNalClassifier.IsVclNalUnit(CodecType.H264, 5);

        Assert.True(isVclNalUnit);
    }

    [Fact]
    public void IsVclNalUnit_returns_false_for_h264_sps()
    {
        bool isVclNalUnit = AnnexBNalClassifier.IsVclNalUnit(CodecType.H264, 7);

        Assert.False(isVclNalUnit);
    }

    [Fact]
    public void IsVclNalUnit_returns_true_for_h265_vcl()
    {
        bool isVclNalUnit = AnnexBNalClassifier.IsVclNalUnit(CodecType.H265, 19);

        Assert.True(isVclNalUnit);
    }

    [Fact]
    public void IsKeyFrameNalUnit_returns_true_for_h264_idr()
    {
        bool isKeyFrameNalUnit = AnnexBNalClassifier.IsKeyFrameNalUnit(CodecType.H264, 5);

        Assert.True(isKeyFrameNalUnit);
    }

    [Fact]
    public void IsKeyFrameNalUnit_returns_false_for_h264_non_idr()
    {
        bool isKeyFrameNalUnit = AnnexBNalClassifier.IsKeyFrameNalUnit(CodecType.H264, 1);

        Assert.False(isKeyFrameNalUnit);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(21)]
    public void IsKeyFrameNalUnit_returns_true_for_h265_idr_or_cra(int nalUnitType)
    {
        bool isKeyFrameNalUnit = AnnexBNalClassifier.IsKeyFrameNalUnit(CodecType.H265, nalUnitType);

        Assert.True(isKeyFrameNalUnit);
    }

    [Fact]
    public void IsKeyFrameNalUnit_returns_false_for_unknown_codec()
    {
        bool isKeyFrameNalUnit = AnnexBNalClassifier.IsKeyFrameNalUnit(CodecType.Unknown, 5);

        Assert.False(isKeyFrameNalUnit);
    }
}
