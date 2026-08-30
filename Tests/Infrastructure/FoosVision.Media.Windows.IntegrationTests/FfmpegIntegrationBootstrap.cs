// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FFmpeg.AutoGen;

namespace FoosVision.Media.Windows.IntegrationTests;

internal static class FfmpegIntegrationBootstrap
{
    private static readonly Lock _Lock = new();
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
            Assert.SkipWhen(LocalIntegrationData.ShouldSkipDirectory(ffmpegRoot), $"Local FFmpeg directory '{ffmpegRoot}' is not available in public test runs.");
            string resolvedFfmpegRoot = ffmpegRoot ?? throw new InvalidOperationException("FFmpeg root was not resolved.");

            ffmpeg.RootPath = resolvedFfmpegRoot;

            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] pathEntries = currentPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (!pathEntries.Contains(resolvedFfmpegRoot, StringComparer.OrdinalIgnoreCase))
            {
                string updatedPath = string.IsNullOrEmpty(currentPath)
                    ? resolvedFfmpegRoot
                    : resolvedFfmpegRoot + Path.PathSeparator + currentPath;
                Environment.SetEnvironmentVariable("PATH", updatedPath);
            }

            _IsInitialized = true;
        }
    }

    private static string? ResolveFfmpegRoot()
    {
        string? configured = Environment.GetEnvironmentVariable("FOOSVISION_FFMPEG_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        configured = Environment.GetEnvironmentVariable("FFMPEG_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        string repoLocal = @"D:\Projects\FoosVision.Integration\ffmpeg\bin";
        if (Directory.Exists(repoLocal))
        {
            return repoLocal;
        }

        return null;
    }
}
