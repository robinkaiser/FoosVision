// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Live;
using MessagePack;

namespace FoosVision.Protocol.Messages.LiveAnalysis;

public enum EncodedReplayCodecMessage
{
    Unknown,
    H264,
    H265,
}

public enum EncodedReplayParameterSetTypeMessage
{
    Invalid,
    VPS,
    SPS,
    PPS,
}

[MessagePackObject(true)]
public record EncodedReplayParameterSetMessage
{
    public EncodedReplayParameterSetTypeMessage Type { get; init; }

    public byte[] Buffer { get; init; } = [];
}

[MessagePackObject(true)]
public record EncodedReplayAccessUnitMessage
{
    public long TimeNs { get; init; }

    public bool IsKeyFrame { get; init; }

    public bool ContainsAllRequiredParameterSets { get; init; }

    public byte[] Buffer { get; init; } = [];
}

[MessagePackObject(true)]
public record ReplayMessage
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

    public IReadOnlyList<EncodedReplayParameterSetMessage> ParameterSets { get; init; } = [];

    public IReadOnlyList<EncodedReplayAccessUnitMessage> AccessUnits { get; init; } = [];
}
