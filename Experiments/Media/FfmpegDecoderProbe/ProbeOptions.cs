// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FfmpegDecoderProbe;

internal enum ProbeCodec
{
    H264,
    H265,
}

internal sealed record ProbeOptions(ProbeCodec Codec, int Width, int Height)
{
    public static string HelpText =>
        """
        Usage:
          FfmpegDecoderProbe --codec <h264|h265> --width <int> --height <int>

        Optional environment variables:
          FOOSVISION_FFMPEG_ROOT
          FFMPEG_ROOT
        """;

    public static ProbeOptions Parse(string[] args)
    {
        if (args.Length == 0)
        {
            return new ProbeOptions(ProbeCodec.H264, 1920, 1080);
        }

        string? codecText = null;
        int? width = null;
        int? height = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg is "--help" or "-h")
            {
                throw new InvalidOperationException(HelpText);
            }

            if (i + 1 >= args.Length)
            {
                throw new InvalidOperationException($"Missing value for '{arg}'.");
            }

            string value = args[++i];

            switch (arg)
            {
                case "--codec":
                    codecText = value;
                    break;
                case "--width":
                    width = int.Parse(value);
                    break;
                case "--height":
                    height = int.Parse(value);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown argument '{arg}'.");
            }
        }

        ProbeCodec codec = codecText?.ToLowerInvariant() switch
        {
            null => ProbeCodec.H264,
            "h264" => ProbeCodec.H264,
            "h265" => ProbeCodec.H265,
            _ => throw new InvalidOperationException($"Unsupported codec '{codecText}'."),
        };

        if (width is null || height is null)
        {
            throw new InvalidOperationException("Both --width and --height are required when arguments are provided.");
        }

        return new ProbeOptions(codec, width.Value, height.Value);
    }
}
