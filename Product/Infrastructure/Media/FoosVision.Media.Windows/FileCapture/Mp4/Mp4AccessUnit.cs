// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Windows.FileCapture.Mp4;

internal class Mp4AccessUnit
{
    public Mp4AccessUnit(long timestampNs, bool isKeyFrame, ReadOnlyMemory<byte> buffer)
    {
        TimestampNs = timestampNs;
        IsKeyFrame = isKeyFrame;
        Buffer = buffer;
    }

    public long TimestampNs { get; }

    public bool IsKeyFrame { get; }

    public ReadOnlyMemory<byte> Buffer { get; }
}
