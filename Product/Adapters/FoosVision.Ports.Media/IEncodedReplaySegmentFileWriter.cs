// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Media;

public interface IEncodedReplaySegmentFileWriter
{
    Task WriteAsync(EncodedReplaySegment segment, string filePath, CancellationToken ct);
}
