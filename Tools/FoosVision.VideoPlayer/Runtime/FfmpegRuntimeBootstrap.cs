// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FFmpeg.AutoGen;
using FoosVision.Common.Logging;

namespace FoosVision.VideoPlayer.Runtime;

public static class FfmpegRuntimeBootstrap
{
    private static readonly Lock _Lock = new();
    private static readonly Source _Log = new("VideoPlayer.FfmpegRuntimeBootstrap");
    private static bool _IsInitialized;

    public static void EnsureInitialized()
    {
        lock (_Lock)
        {
            if (_IsInitialized)
            {
                return;
            }

            string? ffmpegRoot = ResolveFfmpegRoot();
            if (string.IsNullOrWhiteSpace(ffmpegRoot))
            {
                throw new InvalidOperationException(
                    "FFmpeg libraries were not found. Set FOOSVISION_FFMPEG_ROOT or FFMPEG_ROOT to a directory containing the FFmpeg DLLs, or place the DLLs next to the VideoPlayer executable.");
            }

            ffmpeg.RootPath = ffmpegRoot;
            EnsurePathContains(ffmpegRoot);
            _IsInitialized = true;
            _Log.Information("FFmpeg runtime initialized from '{0}'.", ffmpegRoot);
        }
    }

    private static string? ResolveFfmpegRoot()
    {
        string? configured = Environment.GetEnvironmentVariable("FOOSVISION_FFMPEG_ROOT");
        if (IsUsableFfmpegDirectory(configured))
        {
            return configured;
        }

        configured = Environment.GetEnvironmentVariable("FFMPEG_ROOT");
        if (IsUsableFfmpegDirectory(configured))
        {
            return configured;
        }

        string baseDirectory = AppContext.BaseDirectory;
        if (IsUsableFfmpegDirectory(baseDirectory))
        {
            return baseDirectory;
        }

        string localFfmpeg = Path.Combine(baseDirectory, "ffmpeg");
        if (IsUsableFfmpegDirectory(localFfmpeg))
        {
            return localFfmpeg;
        }

        string localFfmpegBin = Path.Combine(localFfmpeg, "bin");
        if (IsUsableFfmpegDirectory(localFfmpegBin))
        {
            return localFfmpegBin;
        }

        string repoLocal = @"D:\Projects\FoosVision.Integration\ffmpeg\bin";
        if (IsUsableFfmpegDirectory(repoLocal))
        {
            return repoLocal;
        }

        return null;
    }

    private static bool IsUsableFfmpegDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        return Directory.EnumerateFiles(path, "avcodec*.dll").Any()
            && Directory.EnumerateFiles(path, "avformat*.dll").Any()
            && Directory.EnumerateFiles(path, "avutil*.dll").Any();
    }

    private static void EnsurePathContains(string directory)
    {
        string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        string[] pathEntries = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pathEntries.Contains(directory, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        string updatedPath = string.IsNullOrEmpty(currentPath)
            ? directory
            : directory + Path.PathSeparator + currentPath;
        Environment.SetEnvironmentVariable("PATH", updatedPath);
    }
}
