// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FFmpeg.AutoGen;
using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Windows.FileCapture.Mp4;

internal static class Mp4CodecMappings
{
    public static CodecType ResolveCodecType(AVCodecID codecId)
    {
        return codecId switch
        {
            AVCodecID.AV_CODEC_ID_H264 => CodecType.H264,
            AVCodecID.AV_CODEC_ID_HEVC => CodecType.H265,
            _ => CodecType.Unknown,
        };
    }

    public static string ResolveBitstreamFilterName(CodecType codec)
    {
        return codec switch
        {
            CodecType.H264 => "h264_mp4toannexb",
            CodecType.H265 => "hevc_mp4toannexb",
            _ => throw new NotSupportedException($"Codec '{codec}' is not supported for MP4 playback."),
        };
    }
}
