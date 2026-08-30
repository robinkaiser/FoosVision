// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Windows.Decoding.Ffmpeg;

internal interface IFfmpegHardwareConfigProvider
{
    IReadOnlyList<FfmpegHardwareDecodeConfig> GetCompatibleHardwareConfigs(CodecType codec);
}
