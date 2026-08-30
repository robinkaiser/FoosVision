// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FFmpeg.AutoGen;

namespace FfmpegDecoderProbe;

internal static class FfmpegBootstrap
{
    private static bool _Initialized;

    public static void EnsureInitialized()
    {
        if (_Initialized)
        {
            return;
        }

        string rootPath = ResolveRootPath()
            ?? throw new InvalidOperationException(
                "FFmpeg libraries were not found. Set FOOSVISION_FFMPEG_ROOT or FFMPEG_ROOT to a directory containing the FFmpeg DLLs.");

        ffmpeg.RootPath = rootPath;
        _Initialized = true;
    }

    private static string? ResolveRootPath()
    {
        string?[] candidates =
        [
            Environment.GetEnvironmentVariable("FOOSVISION_FFMPEG_ROOT"),
            Environment.GetEnvironmentVariable("FFMPEG_ROOT"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", "bin"),
            Path.Combine(AppContext.BaseDirectory),
            @"D:\Projects\FoosVision.Integration\ffmpeg\bin",
        ];

        foreach (string? candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
