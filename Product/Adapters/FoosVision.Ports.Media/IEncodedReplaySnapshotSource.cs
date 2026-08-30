// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Media;

public interface IEncodedReplaySnapshotSource
{
    bool TryGetSnapshot(out EncodedReplaySegment segment);
}
