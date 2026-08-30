// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;

namespace FoosVision.Media.Core.EncodedVideo.AnnexB;

public static class AnnexBNalClassifier
{
    private const int _H264_SPS = 7;
    private const int _H264_PPS = 8;
    private const int _H264_IDR = 5;

    private const int _H265_VPS = 32;
    private const int _H265_SPS = 33;
    private const int _H265_PPS = 34;
    private const int _H265_IDR_W_RADL = 19;
    private const int _H265_IDR_N_LP = 20;

    // Useful random-access types in HEVC (optional but helps some encoders)
    private const int _H265_BLA_W_LP = 16;
    private const int _H265_BLA_W_RADL = 17;
    private const int _H265_BLA_N_LP = 18;
    private const int _H265_CRA_NUT = 21;

    private static readonly Source _Log = new("AnnexBNalClassifier");

    public static CodecType DetectCodecFromHeaderByte(byte headerByte)
    {
        int ut6 = (headerByte >> 1) & 0x3F;

        if (ut6 == _H265_VPS || ut6 == _H265_SPS || ut6 == _H265_PPS)
        {
            _Log.Verbose("GetCodec: H265 detected");
            return CodecType.H265;
        }

        int ut5 = headerByte & 0x1F;

        if (ut5 == _H264_SPS || ut5 == _H264_PPS)
        {
            _Log.Verbose("GetCodec: H264 detected");
            return CodecType.H264;
        }

        return CodecType.Unknown;
    }

    public static int GetNalUnitType(CodecType codec, byte headerByte)
    {
        return codec == CodecType.H264 ?
            (headerByte & 0x1F) :
            ((headerByte >> 1) & 0x3F);
    }

    public static ParameterSetType GetParameterSetType(CodecType codec, int nalUnitType)
    {
        return codec switch
        {
            CodecType.H264 => nalUnitType switch
            {
                _H264_SPS => ParameterSetType.SPS,
                _H264_PPS => ParameterSetType.PPS,
                _ => ParameterSetType.Invalid
            },
            CodecType.H265 => nalUnitType switch
            {
                _H265_VPS => ParameterSetType.VPS,
                _H265_SPS => ParameterSetType.SPS,
                _H265_PPS => ParameterSetType.PPS,
                _ => ParameterSetType.Invalid
            },
            _ => ParameterSetType.Invalid
        };
    }

    public static bool IsVclNalUnit(CodecType codec, int nalUnitType)
    {
        return codec switch
        {
            // H.264 VCL NALs: 1..5 (non-IDR slice..IDR slice)
            CodecType.H264 => nalUnitType >= 1 && nalUnitType <= 5,

            // H.265 VCL NALs: nal_unit_type < 32
            CodecType.H265 => nalUnitType >= 0 && nalUnitType < 32,

            _ => false
        };
    }

    public static bool IsKeyFrameNalUnit(CodecType codec, int nalUnitType)
    {
        return codec switch
        {
            CodecType.H264 => nalUnitType == _H264_IDR,
            CodecType.H265 => nalUnitType == _H265_IDR_W_RADL ||
                              nalUnitType == _H265_IDR_N_LP ||
                              nalUnitType == _H265_CRA_NUT ||
                              nalUnitType == _H265_BLA_W_LP ||
                              nalUnitType == _H265_BLA_W_RADL ||
                              nalUnitType == _H265_BLA_N_LP,
            _ => false
        };
    }
}
