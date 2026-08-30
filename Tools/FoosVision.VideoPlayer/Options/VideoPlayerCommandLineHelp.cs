// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.VideoPlayer.Options;

public static class VideoPlayerCommandLineHelp
{
    public const string Text =
        """
        Usage:
          FoosVision.VideoPlayer --file <path> --codec <h264|h265> --width <int> --height <int> [--encoded-fps <int>] [--decoded-fps <int>] [--decode-mode <prefer-hardware|require-hardware|software-only>]

        Required:
          --file           Path to the input MP4 file.
          --codec          Video codec: h264 or h265.
          --width          Video width in pixels.
          --height         Video height in pixels.

        Optional:
          --encoded-fps    Encoded stream frame rate. Default: 120.
          --decoded-fps    Decoded frame rate for vision processing. Default: 30.
          --decode-mode    Decoder mode: prefer-hardware, require-hardware, software-only. Default: prefer-hardware.
          --help           Show this help text.
        """;
}
