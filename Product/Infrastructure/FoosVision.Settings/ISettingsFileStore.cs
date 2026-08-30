// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings;

public interface ISettingsFileStore
{
    void CreateDirectory(string path);

    bool FileExists(string path);

    string ReadAllText(string path);

    void WriteAllText(string path, string contents);

    bool CanWriteDirectory(string path);
}
