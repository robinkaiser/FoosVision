// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Session.Playback;

public interface IWritableSessionFile
{
    string Path { get; }

    void WriteAllText(string content);
}
