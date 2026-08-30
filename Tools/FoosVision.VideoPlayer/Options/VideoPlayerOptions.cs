// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Media.Windows.FileCapture;

namespace FoosVision.VideoPlayer.Options;

public record VideoPlayerOptions(
    string FilePath,
    CodecType Codec,
    int Width,
    int Height,
    int EncodedFps = 120,
    int DecodedFps = 30,
    WindowsVideoDecoderHardwareMode HardwareMode = WindowsVideoDecoderHardwareMode.PreferHardware)
{
    public FileCameraFeedOptions ToFileCameraFeedOptions()
    {
        return new FileCameraFeedOptions(
            FilePath,
            Codec,
            Width,
            Height,
            EncodedFps,
            DecodedFps,
            HardwareMode: HardwareMode);
    }
}
