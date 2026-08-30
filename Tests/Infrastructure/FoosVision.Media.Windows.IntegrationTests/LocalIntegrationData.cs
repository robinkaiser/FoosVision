// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Windows.IntegrationTests;

internal static class LocalIntegrationData
{
    public static bool ShouldSkipDirectory(string? directory)
    {
        return string.IsNullOrWhiteSpace(directory) ||
            !Directory.Exists(directory);
    }

    public static bool ShouldSkipFile(string path)
    {
        return !File.Exists(path);
    }
}
