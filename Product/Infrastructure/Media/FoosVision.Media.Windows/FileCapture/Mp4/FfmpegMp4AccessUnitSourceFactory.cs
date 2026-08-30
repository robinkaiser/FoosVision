// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Windows.FileCapture.Mp4;

internal class FfmpegMp4AccessUnitSourceFactory : IMp4AccessUnitSourceFactory
{
    public IMp4AccessUnitSource Create() => new FfmpegMp4AccessUnitSource();
}
