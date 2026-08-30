// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.LiveAnalysis;

[MessagePackObject(true)]
public record VisionContextMessage
{
    public byte[] Buffer { get; init; } = [];

    public int Length { get; init; }
}
