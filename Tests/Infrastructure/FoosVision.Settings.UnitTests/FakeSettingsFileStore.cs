// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.UnitTests;

internal class FakeSettingsFileStore : ISettingsFileStore
{
    public Dictionary<string, string> Files { get; } = [];

    public HashSet<string> CreatedDirectories { get; } = [];

    public HashSet<string> UnwritableDirectories { get; init; } = [];

    public void CreateDirectory(string path)
    {
        CreatedDirectories.Add(path);
    }

    public bool FileExists(string path)
    {
        return Files.ContainsKey(path);
    }

    public string ReadAllText(string path)
    {
        return Files[path];
    }

    public void WriteAllText(string path, string contents)
    {
        Files[path] = contents;
    }

    public bool CanWriteDirectory(string path)
    {
        CreatedDirectories.Add(path);
        return !UnwritableDirectories.Contains(path);
    }
}
