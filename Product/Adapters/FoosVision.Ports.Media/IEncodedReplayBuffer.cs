// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Media;

public enum EncodedReplayCodec
{
    Unknown,
    H264,
    H265,
}

public enum EncodedReplayParameterSetType
{
    Invalid,
    VPS,
    SPS,
    PPS,
}

public record EncodedReplayParameterSet(
    EncodedReplayParameterSetType Type,
    byte[] Buffer);

public record EncodedReplayAccessUnit(
    long TimeNs,
    bool IsKeyFrame,
    bool ContainsAllRequiredParameterSets,
    byte[] Buffer);

public record EncodedReplaySegment(
    EncodedReplayCodec Codec,
    long StartTimeNs,
    long EndTimeNs,
    IReadOnlyList<EncodedReplayParameterSet> ParameterSets,
    IReadOnlyList<EncodedReplayAccessUnit> AccessUnits);

public interface IEncodedReplayBuffer
{
    bool TryGetReplaySegment(long startTimeNs, long endTimeNs, out EncodedReplaySegment segment);
}
