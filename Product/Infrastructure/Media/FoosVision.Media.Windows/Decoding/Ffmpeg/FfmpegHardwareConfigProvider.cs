// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FFmpeg.AutoGen;
using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Windows.Decoding.Ffmpeg;

internal class FfmpegHardwareConfigProvider : IFfmpegHardwareConfigProvider
{
    private const int _CodecHwConfigMethodHardwareDeviceContext = 0x01;

    private static readonly AVHWDeviceType[] _PreferredDeviceTypes =
    [
        AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
        AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2,
    ];

    public IReadOnlyList<FfmpegHardwareDecodeConfig> GetCompatibleHardwareConfigs(CodecType codec)
    {
        AVCodecID codecId = codec switch
        {
            CodecType.H264 => AVCodecID.AV_CODEC_ID_H264,
            CodecType.H265 => AVCodecID.AV_CODEC_ID_HEVC,
            _ => AVCodecID.AV_CODEC_ID_NONE,
        };

        if (codecId == AVCodecID.AV_CODEC_ID_NONE)
        {
            return [];
        }

        unsafe
        {
            AVCodec* decoder = ffmpeg.avcodec_find_decoder(codecId);
            if (decoder == null)
            {
                return [];
            }

            List<FfmpegHardwareDecodeConfig> result = [];

            for (int index = 0; ; index++)
            {
                AVCodecHWConfig* config = ffmpeg.avcodec_get_hw_config(decoder, index);
                if (config == null)
                {
                    break;
                }

                if ((config->methods & _CodecHwConfigMethodHardwareDeviceContext) == 0)
                {
                    continue;
                }

                if (Array.IndexOf(_PreferredDeviceTypes, config->device_type) < 0)
                {
                    continue;
                }

                result.Add(new FfmpegHardwareDecodeConfig(config->device_type, config->pix_fmt));
            }

            result.Sort(static (left, right) =>
            {
                int leftIndex = Array.IndexOf(_PreferredDeviceTypes, left.DeviceType);
                int rightIndex = Array.IndexOf(_PreferredDeviceTypes, right.DeviceType);
                return leftIndex.CompareTo(rightIndex);
            });

            return result;
        }
    }
}
