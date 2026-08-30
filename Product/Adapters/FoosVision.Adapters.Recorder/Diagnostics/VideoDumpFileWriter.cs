// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Recorder.Diagnostics;

public class VideoDumpFileWriter : IVideoDumpWriter
{
    private readonly VideoDumpFileWriterOptions _Options;
    private readonly IEncodedReplaySegmentFileWriter _SegmentWriter;
    private readonly IVideoDumpFileStore _FileStore;
    private readonly VideoDumpRetentionPolicy _RetentionPolicy;
    private readonly Func<DateTimeOffset> _Now;

    public VideoDumpFileWriter(
        VideoDumpFileWriterOptions options,
        IEncodedReplaySegmentFileWriter segmentWriter,
        IVideoDumpFileStore fileStore,
        Func<DateTimeOffset>? now = null)
    {
        _Options = options;
        _SegmentWriter = segmentWriter;
        _FileStore = fileStore;
        _RetentionPolicy = new VideoDumpRetentionPolicy(_FileStore);
        _Now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task WriteAsync(VideoDumpRequest request, CancellationToken ct)
    {
        if (!_Options.Enabled)
        {
            return;
        }

        _FileStore.CreateDirectory(_Options.VideosDirectory);

        string finalPath = Path.Combine(_Options.VideosDirectory, request.FileName);
        string temporaryPath = finalPath + ".tmp";

        try
        {
            _FileStore.DeleteFile(temporaryPath);
            await _SegmentWriter.WriteAsync(request.Segment, temporaryPath, ct).ConfigureAwait(false);
            _FileStore.MoveFile(temporaryPath, finalPath, overwrite: true);
            _RetentionPolicy.Apply(
                _Options.VideosDirectory,
                _Options.RetentionDays,
                _Options.MaxTotalSizeBytes,
                _Now());
        }
        catch
        {
            _FileStore.DeleteFile(temporaryPath);
            throw;
        }
    }
}
