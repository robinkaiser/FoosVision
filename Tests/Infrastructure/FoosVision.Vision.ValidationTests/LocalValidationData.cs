// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.ValidationTests;

internal static class LocalValidationData
{
    public static bool ShouldSkipDirectory(string directory)
    {
        return !Directory.Exists(directory);
    }
}
