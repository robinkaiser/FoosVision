// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.LiveAnalysis;

[MessagePackObject(true)]
public record BallDetectionMaskMessage
{
    public ulong FrameId { get; init; }

    public long TimestampNs { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public byte[] Buffer { get; init; } = [];

    public int Length { get; init; }
}
