// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Media.Core.EncodedVideo.AnnexB;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Core.EncodedVideo;

public class EncodedAccessUnitBuffer : IEncodedAccessUnitSink
{
    private const int _MAX_NALS_PER_COMPLETION = 32;

    private static readonly Source _Log = new("EncodedAccessUnitBuffer");

    private readonly int _Capacity;
    private readonly int _MaxChunkSize;
    private readonly byte[] _Buffer;
    private readonly List<EncodedAccessUnit> _UnitBuffer;
    private readonly Dictionary<ParameterSetType, ParameterSet> _Header;
    private readonly AnnexBNalRange[] _ParsedNalRanges;

    private Action<EncodedAccessUnit>? _EncodedUnitReadyNotificationSink;

    public EncodedAccessUnitBuffer(int capacity, int maxChunkSize)
    {
        _Capacity = capacity;
        _MaxChunkSize = maxChunkSize;
        _UnitBuffer = [];
        _Header = [];

        _Buffer = new byte[capacity];
        _ParsedNalRanges = new AnnexBNalRange[_MAX_NALS_PER_COMPLETION];
        Reset();
    }

    public byte[] Buffer => _Buffer;

    public int Offset { get; private set; }

    public CodecType Codec { get; private set; }

    public bool HasHeader { get; private set; }

    public IEnumerable<ParameterSet> Header => _Header.Values;

    public void SetEncodedUnitReadyNotificationSink(Action<EncodedAccessUnit> sink)
        => _EncodedUnitReadyNotificationSink = sink;

    public void Completed(long timeNs, int size)
    {
        if (size <= 0) return;

        if (size > _MaxChunkSize)
        {
            _Log.Warning($"Completed: Unexpected size of {size}. Max size expected: {_MaxChunkSize}");
            return;
        }

        int start = Offset;
        int endExclusive = Offset + size;

        if (endExclusive > _Capacity)
        {   // Defensive (should not happen if producer respects remaining space)
            _Log.Error($"Completed: Chunk exceeds capacity. Offset={Offset}, size={size}, capacity={_Capacity}");
            return;
        }

        var count = AnnexBParser.FindNals(_Buffer, start, endExclusive, _ParsedNalRanges, _MAX_NALS_PER_COMPLETION);

        if (count == 0)
        {
            _Log.Warning($"Completed: Invalid NAL (no start code). Time: {timeNs}");
            return;
        }

        if (count == -1)
        {
            _Log.Warning($"Completed: Too many NALs inside this unit. Time: {timeNs}");
            return;
        }

        bool sawVclNal = false;
        bool isKeyFrame = false;
        bool containsVps = false;
        bool containsSps = false;
        bool containsPps = false;

        for (int i = 0; i < count; i++)
        {
            var nal = _ParsedNalRanges[i];

            byte headerByte = _Buffer[nal.HeaderOffset];

            if (Codec == CodecType.Unknown)
            {   // Detect codec if unknown (only flips when we see VPS/SPS/PPS)
                Codec = AnnexBNalClassifier.DetectCodecFromHeaderByte(headerByte);

                // If still unknown, we keep scanning; we discard the chunk later if we never detect codec.
            }

            if (Codec != CodecType.Unknown)
            {
                int nalTypeNum = AnnexBNalClassifier.GetNalUnitType(Codec, headerByte);

                // Header NALs: store them (even if chunk also contains VCL)
                ParameterSetType headerType = AnnexBNalClassifier.GetParameterSetType(Codec, nalTypeNum);

                if (headerType != ParameterSetType.Invalid)
                {
                    StoreHeader(headerType, nal.StartOffset, nal.EndOffsetExclusive);

                    switch (headerType)
                    {
                        case ParameterSetType.VPS:
                            containsVps = true;
                            break;
                        case ParameterSetType.SPS:
                            containsSps = true;
                            break;
                        case ParameterSetType.PPS:
                            containsPps = true;
                            break;
                    }
                }

                // VCL detection (frame content)
                if (AnnexBNalClassifier.IsVclNalUnit(Codec, nalTypeNum))
                {
                    sawVclNal = true;
                }

                // Keyframe detection: any IDR (and HEVC CRA/BLA treated as keyframe here)
                if (AnnexBNalClassifier.IsKeyFrameNalUnit(Codec, nalTypeNum))
                {
                    isKeyFrame = true;
                }
            }
        }

        if (Codec == CodecType.Unknown)
        {   // If we never detected codec, discard (don’t advance Offset so producer overwrites)
            return;
        }

        // Update HasHeader after we may have stored headers from this chunk
        if (!HasHeader)
        {
            bool hasVPS = _Header.ContainsKey(ParameterSetType.VPS);
            bool hasSPS = _Header.ContainsKey(ParameterSetType.SPS);
            bool hasPPS = _Header.ContainsKey(ParameterSetType.PPS);

            HasHeader = Codec switch
            {
                CodecType.H264 => hasSPS && hasPPS,
                CodecType.H265 => hasVPS && hasSPS && hasPPS,
                _ => false
            };
        }

        if (!HasHeader || !sawVclNal)
        {   // If this chunk is header-only (or non-VCL), or header isn’t complete yet, don’t store it as a frame.
            // Leave Offset unchanged so it’s overwritten.
            return;
        }

        // Remove any stored units whose byte range will be overwritten by this new chunk
        if (_UnitBuffer.Count != 0)
        {
            int writeStart = Offset;
            int writeEnd = Offset + size;

            _UnitBuffer.RemoveAll(n =>
            {
                int nStart = n.Offset;
                int nEnd = n.Offset + n.Size;
                return nStart < writeEnd && writeStart < nEnd;
            });
        }

        bool containsAllRequiredParameterSets = Codec switch
        {
            CodecType.H264 => containsSps && containsPps,
            CodecType.H265 => containsVps && containsSps && containsPps,
            _ => false,
        };

        // Store as one access unit
        EncodedAccessUnit unit = new(timeNs, isKeyFrame, containsAllRequiredParameterSets, Offset, size);
        _UnitBuffer.Add(unit);

        Offset += size;

        // Overflow handling (ring behavior)
        if ((_Capacity - Offset) < _MaxChunkSize)
        {
            Offset = 0;
        }

        _EncodedUnitReadyNotificationSink?.Invoke(unit);
    }

    public bool TryGetReplaySegment(long startTimeNs, long endTimeNs, out EncodedReplaySegment segment)
    {
        if (_UnitBuffer.Count == 0 ||
            endTimeNs < startTimeNs)
        {
            LogReplaySegmentUnavailable("empty buffer or invalid range", startTimeNs, endTimeNs);
            segment = CreateEmptyReplaySegment(startTimeNs, endTimeNs);
            return false;
        }

        if (!HasHeader)
        {
            LogReplaySegmentUnavailable("header missing", startTimeNs, endTimeNs);
            segment = CreateEmptyReplaySegment(startTimeNs, endTimeNs);
            return false;
        }

        int startIndex = FindKeyFrameAtOrBefore(startTimeNs);

        if (startIndex < 0)
        {
            LogReplaySegmentUnavailable("no keyframe at or before requested start", startTimeNs, endTimeNs);
            segment = CreateEmptyReplaySegment(startTimeNs, endTimeNs);
            return false;
        }

        int endIndex = startIndex;

        while (endIndex < _UnitBuffer.Count &&
               _UnitBuffer[endIndex].TimeNs < endTimeNs)
        {
            endIndex++;
        }

        if (endIndex == _UnitBuffer.Count)
        {
            endIndex--;
        }

        segment = CreateSegment(
            startIndex,
            endIndex,
            _UnitBuffer[startIndex].TimeNs,
            _UnitBuffer[endIndex].TimeNs);

        return true;
    }

    public bool TryGetSnapshot(out EncodedReplaySegment segment)
    {
        if (_UnitBuffer.Count == 0)
        {
            _Log.Warning("TryGetSnapshot: Failed. Entry count: 0");
            segment = CreateEmptyReplaySegment(0, 0);
            return false;
        }

        if (!HasHeader)
        {
            _Log.Warning("TryGetSnapshot: Header missing");
            segment = CreateEmptyReplaySegment(_UnitBuffer[0].TimeNs, _UnitBuffer[^1].TimeNs);
            return false;
        }

        int startIndex = 0;

        while (startIndex < _UnitBuffer.Count &&
              !_UnitBuffer[startIndex].IsKeyFrame)
        {
            startIndex++;
        }

        if (startIndex == _UnitBuffer.Count)
        {
            segment = CreateEmptyReplaySegment(_UnitBuffer[0].TimeNs, _UnitBuffer[^1].TimeNs);
            return false;
        }

        int endIndex = _UnitBuffer.Count - 1;

        segment = CreateSegment(
            startIndex,
            endIndex,
            _UnitBuffer[startIndex].TimeNs,
            _UnitBuffer[endIndex].TimeNs);
        return true;
    }

    public void Reset()
    {
        _UnitBuffer.Clear();
        _Header.Clear();

        Codec = CodecType.Unknown;
        Offset = 0;
        HasHeader = false;
    }

    private void StoreHeader(ParameterSetType headerType, int start, int endExclusive)
    {
        if (headerType == ParameterSetType.Invalid) return;

        if (_Header.TryGetValue(headerType, out ParameterSet? entry) && entry != null)
        {
            if (!Buffer.AsSpan(start, endExclusive - start).SequenceEqual(entry.Buffer))
            {
                _Log.Error($"Completed: New header {headerType} differs from previous one. This is unexpected!");
            }
        }
        else
        {
            ParameterSet newEntry = new(headerType, Buffer[start..endExclusive]);
            _Header.Add(headerType, newEntry);
        }
    }

    private EncodedReplaySegment CreateSegment(int startIndex, int endIndex, long startTimeNs, long endTimeNs)
    {
        var parameterSets = _Header.Values
            .Select(p => new EncodedReplayParameterSet(
                ConvertParameterSetType(p.Type),
                [.. p.Buffer]))
            .ToList();

        List<EncodedReplayAccessUnit> accessUnits = [];

        for (int i = startIndex; i <= endIndex; i++)
        {
            EncodedAccessUnit unit = _UnitBuffer[i];
            accessUnits.Add(new EncodedReplayAccessUnit(
                unit.TimeNs,
                unit.IsKeyFrame,
                unit.ContainsAllRequiredParameterSets,
                _Buffer[unit.Offset..(unit.Offset + unit.Size)]));
        }

        return new EncodedReplaySegment(
            ConvertCodec(Codec),
            startTimeNs,
            endTimeNs,
            parameterSets,
            accessUnits);
    }

    private int FindKeyFrameAtOrBefore(long startTimeNs)
    {
        if (_UnitBuffer.Count == 0 ||
            startTimeNs < _UnitBuffer[0].TimeNs ||
            startTimeNs > _UnitBuffer[^1].TimeNs)
        {
            return -1;
        }

        for (int i = _UnitBuffer.Count - 1; i >= 0; i--)
        {
            EncodedAccessUnit unit = _UnitBuffer[i];

            if (unit.TimeNs > startTimeNs)
            {
                continue;
            }

            if (unit.IsKeyFrame)
            {
                return i;
            }
        }

        return -1;
    }

    private void LogReplaySegmentUnavailable(string reason, long startTimeNs, long endTimeNs)
    {
        long? firstUnitTimeNs = null;
        long? lastUnitTimeNs = null;
        long? firstKeyFrameTimeNs = null;
        long? lastKeyFrameTimeNs = null;
        int keyFrameCount = 0;

        if (_UnitBuffer.Count != 0)
        {
            firstUnitTimeNs = _UnitBuffer[0].TimeNs;
            lastUnitTimeNs = _UnitBuffer[^1].TimeNs;
        }

        foreach (EncodedAccessUnit unit in _UnitBuffer)
        {
            if (!unit.IsKeyFrame)
            {
                continue;
            }

            firstKeyFrameTimeNs ??= unit.TimeNs;
            lastKeyFrameTimeNs = unit.TimeNs;
            keyFrameCount++;
        }

        _Log.Warning(
            "TryGetReplaySegment failed: {Reason}. RequestedStartNs={RequestedStartNs} RequestedEndNs={RequestedEndNs} UnitCount={UnitCount} Codec={Codec} HasHeader={HasHeader} FirstUnitTimeNs={FirstUnitTimeNs} LastUnitTimeNs={LastUnitTimeNs} KeyFrameCount={KeyFrameCount} FirstKeyFrameTimeNs={FirstKeyFrameTimeNs} LastKeyFrameTimeNs={LastKeyFrameTimeNs} Offset={Offset} Capacity={Capacity}",
            reason,
            startTimeNs,
            endTimeNs,
            _UnitBuffer.Count,
            Codec,
            HasHeader,
            firstUnitTimeNs,
            lastUnitTimeNs,
            keyFrameCount,
            firstKeyFrameTimeNs,
            lastKeyFrameTimeNs,
            Offset,
            _Capacity);
    }

    private static EncodedReplaySegment CreateEmptyReplaySegment(long startTimeNs, long endTimeNs)
        => new(
            EncodedReplayCodec.Unknown,
            startTimeNs,
            endTimeNs,
            [],
            []);

    private static EncodedReplayCodec ConvertCodec(CodecType codec) => codec switch
    {
        CodecType.H264 => EncodedReplayCodec.H264,
        CodecType.H265 => EncodedReplayCodec.H265,
        _ => EncodedReplayCodec.Unknown,
    };

    private static EncodedReplayParameterSetType ConvertParameterSetType(ParameterSetType type) => type switch
    {
        ParameterSetType.VPS => EncodedReplayParameterSetType.VPS,
        ParameterSetType.SPS => EncodedReplayParameterSetType.SPS,
        ParameterSetType.PPS => EncodedReplayParameterSetType.PPS,
        _ => EncodedReplayParameterSetType.Invalid,
    };
}
