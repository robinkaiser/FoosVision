// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings;

public class SettingsFileStore : ISettingsFileStore
{
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public bool FileExists(string path)
    {
        return File.Exists(path);
    }

    public string ReadAllText(string path)
    {
        return File.ReadAllText(path);
    }

    public void WriteAllText(string path, string contents)
    {
        File.WriteAllText(path, contents);
    }

    public bool CanWriteDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            string testFilePath = Path.Combine(path, $".write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFilePath, string.Empty);
            File.Delete(testFilePath);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
