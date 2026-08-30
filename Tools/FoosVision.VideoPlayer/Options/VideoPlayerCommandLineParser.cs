// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;

namespace FoosVision.VideoPlayer.Options;

public class VideoPlayerCommandLineParser
{
    public VideoPlayerCommandLineParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
        {
            return Failure("Missing required arguments.");
        }

        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase))
            {
                return new VideoPlayerCommandLineParseResult(true, true, null, null);
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                return Failure($"Unexpected argument '{arg}'.");
            }

            if (i + 1 >= args.Length)
            {
                return Failure($"Missing value for argument '{arg}'.");
            }

            if (values.ContainsKey(arg))
            {
                return Failure($"Argument '{arg}' was specified more than once.");
            }

            values[arg] = args[++i];
        }

        if (!values.TryGetValue("--file", out string? filePath))
        {
            return Failure("Missing required argument '--file'.");
        }

        if (!values.TryGetValue("--codec", out string? codecText) || !TryParseCodec(codecText, out CodecType codec))
        {
            return Failure("Argument '--codec' must be 'h264' or 'h265'.");
        }

        if (!TryGetRequiredInt32(values, "--width", out int width, out string? widthError))
        {
            return Failure(widthError!);
        }

        if (!TryGetRequiredInt32(values, "--height", out int height, out string? heightError))
        {
            return Failure(heightError!);
        }

        if (!TryGetOptionalInt32(values, "--encoded-fps", 120, out int encodedFps, out string? encodedFpsError))
        {
            return Failure(encodedFpsError!);
        }

        if (!TryGetOptionalInt32(values, "--decoded-fps", 30, out int decodedFps, out string? decodedFpsError))
        {
            return Failure(decodedFpsError!);
        }

        if (!TryGetDecodeMode(values, out WindowsVideoDecoderHardwareMode hardwareMode, out string? hardwareError))
        {
            return Failure(hardwareError!);
        }

        VideoPlayerOptions options = new(filePath, codec, width, height, encodedFps, decodedFps, hardwareMode);

        try
        {
            options.ToFileCameraFeedOptions().Validate();
        }
        catch (Exception ex) when (ex is ArgumentException or ArgumentOutOfRangeException or NotSupportedException)
        {
            return Failure(ex.Message);
        }

        return new VideoPlayerCommandLineParseResult(true, false, options, null);
    }

    private static bool TryGetRequiredInt32(IReadOnlyDictionary<string, string> values, string key, out int value, out string? errorMessage)
    {
        if (!values.TryGetValue(key, out string? text))
        {
            value = default;
            errorMessage = $"Missing required argument '{key}'.";
            return false;
        }

        if (!int.TryParse(text, out value))
        {
            errorMessage = $"Argument '{key}' must be a valid integer.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TryGetOptionalInt32(IReadOnlyDictionary<string, string> values, string key, int defaultValue, out int value, out string? errorMessage)
    {
        if (!values.TryGetValue(key, out string? text))
        {
            value = defaultValue;
            errorMessage = null;
            return true;
        }

        if (!int.TryParse(text, out value))
        {
            errorMessage = $"Argument '{key}' must be a valid integer.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    private static bool TryGetDecodeMode(
        IReadOnlyDictionary<string, string> values,
        out WindowsVideoDecoderHardwareMode value,
        out string? errorMessage)
    {
        values.TryGetValue("--decode-mode", out string? text);

        if (text is null)
        {
            value = WindowsVideoDecoderHardwareMode.PreferHardware;
            errorMessage = null;
            return true;
        }

        if (string.Equals(text, "prefer-hardware", StringComparison.OrdinalIgnoreCase))
        {
            value = WindowsVideoDecoderHardwareMode.PreferHardware;
            errorMessage = null;
            return true;
        }

        if (string.Equals(text, "require-hardware", StringComparison.OrdinalIgnoreCase))
        {
            value = WindowsVideoDecoderHardwareMode.RequireHardware;
            errorMessage = null;
            return true;
        }

        if (string.Equals(text, "software-only", StringComparison.OrdinalIgnoreCase))
        {
            value = WindowsVideoDecoderHardwareMode.SoftwareOnly;
            errorMessage = null;
            return true;
        }

        value = default;
        errorMessage = "Argument '--decode-mode' must be 'prefer-hardware', 'require-hardware' or 'software-only'.";
        return false;
    }

    private static bool TryParseCodec(string text, out CodecType codec)
    {
        if (string.Equals(text, "h264", StringComparison.OrdinalIgnoreCase))
        {
            codec = CodecType.H264;
            return true;
        }

        if (string.Equals(text, "h265", StringComparison.OrdinalIgnoreCase))
        {
            codec = CodecType.H265;
            return true;
        }

        codec = CodecType.Unknown;
        return false;
    }

    private static VideoPlayerCommandLineParseResult Failure(string errorMessage)
    {
        return new VideoPlayerCommandLineParseResult(false, false, null, errorMessage);
    }
}
