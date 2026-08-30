// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FFmpeg.AutoGen;

namespace FoosVision.Media.Windows.Decoding.Ffmpeg;

internal readonly record struct FfmpegHardwareDecodeConfig(
    AVHWDeviceType DeviceType,
    AVPixelFormat PixelFormat);
