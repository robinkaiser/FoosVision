// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision;

public static class AppRolePaths
{
    private const string _RecorderDirectoryName = "FoosVision.Recorder";
    private const string _ViewerDirectoryName = "FoosVision.Viewer";

    public static string? GetPreferredRecorderAppFilesPath()
    {
        return GetPreferredRoleAppFilesPath(_RecorderDirectoryName);
    }

    public static string GetRecorderAppDataPath()
    {
        return GetRoleAppDataPath(_RecorderDirectoryName);
    }

    public static string? GetPreferredViewerAppFilesPath()
    {
        return GetPreferredRoleAppFilesPath(_ViewerDirectoryName);
    }

    public static string GetViewerAppDataPath()
    {
        return GetRoleAppDataPath(_ViewerDirectoryName);
    }

    private static string? GetPreferredRoleAppFilesPath(string roleDirectoryName)
    {
        string? externalAppFilesDirectory = AppPlatformPaths.GetExternalAppFilesDirectory();

        return string.IsNullOrWhiteSpace(externalAppFilesDirectory)
            ? null
            : Path.Combine(externalAppFilesDirectory, roleDirectoryName);
    }

    private static string GetRoleAppDataPath(string roleDirectoryName)
    {
        return Path.Combine(FileSystem.Current.AppDataDirectory, roleDirectoryName);
    }
}
