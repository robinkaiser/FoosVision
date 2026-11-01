// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.LiveAnalysis;

[MessagePackObject(true)]
public record ReplayReturnToLiveMessage
{
    public Guid SessionId { get; init; }
    public ulong TriggerFrameId { get; init; }
    public long TriggerTimestampNs { get; init; }
}
