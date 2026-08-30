// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Windows.Decoding.Ffmpeg;

internal interface IFfmpegDecoderSessionFactory
{
    IFfmpegDecoderSession Create(FfmpegDecoderOptions options);
}
