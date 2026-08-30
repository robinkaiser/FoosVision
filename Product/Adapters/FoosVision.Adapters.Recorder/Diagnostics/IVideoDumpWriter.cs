// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Recorder.Diagnostics;

public enum VideoDumpSessionKind
{
    Installation,
    Game,
}

public record VideoDumpRequest(
    VideoDumpSessionKind SessionKind,
    DateTimeOffset CreatedAt,
    string FileName,
    EncodedReplaySegment Segment);

public interface IVideoDumpWriter
{
    Task WriteAsync(VideoDumpRequest request, CancellationToken ct);
}
