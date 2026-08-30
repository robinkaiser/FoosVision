// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Recorder.Diagnostics;

public class VideoDumpRetentionPolicy
{
    private readonly IVideoDumpFileStore _FileStore;

    public VideoDumpRetentionPolicy(IVideoDumpFileStore fileStore)
    {
        _FileStore = fileStore;
    }

    public void Apply(
        string videosDirectory,
        int retentionDays,
        long maxTotalSizeBytes,
        DateTimeOffset now)
    {
        IReadOnlyList<VideoDumpFileEntry> retainedFiles = DeleteExpiredFiles(videosDirectory, retentionDays, now);
        DeleteFilesOverSizeLimit(retainedFiles, maxTotalSizeBytes);
    }

    private IReadOnlyList<VideoDumpFileEntry> DeleteExpiredFiles(
        string videosDirectory,
        int retentionDays,
        DateTimeOffset now)
    {
        IReadOnlyList<VideoDumpFileEntry> files = GetVideoFiles(videosDirectory);

        if (retentionDays <= 0)
        {
            return files;
        }

        DateTimeOffset cutoff = now.AddDays(-retentionDays);

        foreach (VideoDumpFileEntry file in files.Where(x => x.LastWriteTimeUtc < cutoff))
        {
            _FileStore.DeleteFile(file.Path);
        }

        return [.. files.Where(x => x.LastWriteTimeUtc >= cutoff)];
    }

    private void DeleteFilesOverSizeLimit(
        IReadOnlyList<VideoDumpFileEntry> files,
        long maxTotalSizeBytes)
    {
        if (maxTotalSizeBytes <= 0)
        {
            return;
        }

        long totalSizeBytes = files.Sum(x => x.SizeBytes);

        foreach (VideoDumpFileEntry file in files.OrderBy(x => x.LastWriteTimeUtc))
        {
            if (totalSizeBytes <= maxTotalSizeBytes)
            {
                return;
            }

            _FileStore.DeleteFile(file.Path);
            totalSizeBytes -= file.SizeBytes;
        }
    }

    private IReadOnlyList<VideoDumpFileEntry> GetVideoFiles(string videosDirectory)
    {
        return [.. _FileStore
            .EnumerateFiles(videosDirectory, "*.mp4")
            .OrderBy(x => x.LastWriteTimeUtc)];
    }
}
