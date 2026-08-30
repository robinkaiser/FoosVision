// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Recorder.Diagnostics;

public record VideoDumpFileEntry(
    string Path,
    DateTimeOffset LastWriteTimeUtc,
    long SizeBytes);

public interface IVideoDumpFileStore
{
    void CreateDirectory(string path);

    IReadOnlyList<VideoDumpFileEntry> EnumerateFiles(string directory, string searchPattern);

    void DeleteFile(string path);

    void MoveFile(string sourcePath, string destinationPath, bool overwrite);
}
