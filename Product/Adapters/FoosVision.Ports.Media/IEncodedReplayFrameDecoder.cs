// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Ports.Media;

public record EncodedReplayDecodeRequest(
    EncodedReplayCodec Codec,
    IReadOnlyList<EncodedReplayParameterSet> ParameterSets,
    IReadOnlyList<EncodedReplayAccessUnit> AccessUnits);

public record DecodedReplayFrame(
    IYuvFrameHandle Frame);

public interface IEncodedReplayFrameDecoder
{
    IAsyncEnumerable<DecodedReplayFrame> Decode(EncodedReplayDecodeRequest replay, CancellationToken ct);
}
