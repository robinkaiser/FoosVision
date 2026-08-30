// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Globalization;
using FoosVision.Common.Logging;
using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Recorder.Diagnostics;

public class VideoDumpOrchestrator : IVideoDumpOrchestrator
{
    private const long _NanosecondsPerSecond = 1_000_000_000L;

    private static readonly Source _Log = new("Adapters.Recorder.Diagnostics.VideoDumpOrchestrator");

    private readonly IEncodedReplaySnapshotSource _SnapshotSource;
    private readonly IVideoDumpWriter _Writer;
    private readonly IVideoDumpBackgroundQueue _BackgroundQueue;
    private readonly Func<DateTimeOffset> _Now;
    private readonly bool _Enabled;

    public VideoDumpOrchestrator(
        IEncodedReplaySnapshotSource snapshotSource,
        IVideoDumpWriter writer,
        IVideoDumpBackgroundQueue backgroundQueue,
        Func<DateTimeOffset>? now = null,
        bool enabled = true)
    {
        _SnapshotSource = snapshotSource;
        _Writer = writer;
        _BackgroundQueue = backgroundQueue;
        _Now = now ?? (() => DateTimeOffset.Now);
        _Enabled = enabled;
    }

    public bool TryScheduleDump(VideoDumpSessionKind sessionKind)
    {
        if (!_Enabled)
        {
            return false;
        }

        EncodedReplaySegment segment;

        try
        {
            if (!_SnapshotSource.TryGetSnapshot(out segment))
            {
                _Log.Warning("Skipping {SessionKind} video dump because no usable encoded snapshot is available.", sessionKind);
                return false;
            }
        }
        catch (Exception ex)
        {
            _Log.Warning("Skipping {SessionKind} video dump because snapshot failed: {Exception}", sessionKind, ex);
            return false;
        }

        DateTimeOffset createdAt = _Now();
        var request = new VideoDumpRequest(
            sessionKind,
            createdAt,
            CreateFileName(sessionKind, createdAt, segment),
            segment);

        _BackgroundQueue.Enqueue(ct => WriteAsync(request, ct));
        return true;
    }

    private async Task WriteAsync(VideoDumpRequest request, CancellationToken ct)
    {
        try
        {
            await _Writer.WriteAsync(request, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _Log.Warning("Video dump failed for {FileName}: {Exception}", request.FileName, ex);
        }
    }

    private static string CreateFileName(
        VideoDumpSessionKind sessionKind,
        DateTimeOffset createdAt,
        EncodedReplaySegment segment)
    {
        string sessionPart = sessionKind switch
        {
            VideoDumpSessionKind.Installation => "installation",
            VideoDumpSessionKind.Game => "game",
            _ => "unknown",
        };

        long durationNs = Math.Max(0L, segment.EndTimeNs - segment.StartTimeNs);
        long durationSeconds = durationNs / _NanosecondsPerSecond;
        string timestamp = createdAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        return $"recorder-{sessionPart}-{timestamp}-buffer-{durationSeconds}s.mp4";
    }
}
