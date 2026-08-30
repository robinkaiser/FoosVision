// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Live;
using MessagePack;

namespace FoosVision.Protocol.Messages.LiveAnalysis;

[MessagePackObject(true)]
public record BallPositionMessage
{
    public double X { get; init; }

    public double Y { get; init; }
}

[MessagePackObject(true)]
public record ReplayStartedMessage
{
    public ulong TriggerFrameId { get; init; }

    public long TriggerTimestampNs { get; init; }

    public ulong AnchorFrameId { get; init; }

    public long AnchorTimestampNs { get; init; }

    public BallPositionMessage AnchorPosition { get; init; } = new();

    public PossessionMessage AnchorPossession { get; init; } = PossessionMessage.None;

    public int AnchorPossessionTimeMs { get; init; }

    public long ReplayStartTimestampNs { get; init; }

    public long ReplayEndTimestampNs { get; init; }

    public EncodedReplayCodecMessage Codec { get; init; }

    public int ParameterSetCount { get; init; }

    public int AccessUnitCount { get; init; }

    public int AccessUnitBytes { get; init; }
}
