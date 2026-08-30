// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Diagnostics;

namespace FoosVision.Recorder.Composition.Diagnostics;

public class VideoDumpFileSystemFileStore : IVideoDumpFileStore
{
    public void CreateDirectory(string path)
    {
        Directory.CreateDirectory(path);
    }

    public IReadOnlyList<VideoDumpFileEntry> EnumerateFiles(string directory, string searchPattern)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return [.. Directory
            .EnumerateFiles(directory, searchPattern)
            .Select(path =>
            {
                FileInfo info = new(path);
                return new VideoDumpFileEntry(
                    info.FullName,
                    new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero),
                    info.Length);
            })];
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public void MoveFile(string sourcePath, string destinationPath, bool overwrite)
    {
        File.Move(sourcePath, destinationPath, overwrite);
    }
}
