// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using AndroidApplication = Android.App.Application;

namespace FoosVision;

public static partial class AppPlatformPaths
{
    public static partial string? GetExternalAppFilesDirectory()
    {
        return AndroidApplication.Context.GetExternalFilesDir(null)?.AbsolutePath;
    }
}
