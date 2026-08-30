// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Core.UnitTests;

public class EncodedAccessUnitBufferTests
{
    private const int _Capacity = 50;
    private const int _MaxChunkSize = 20;

    private static readonly byte[] _Invalid = [0x0, 0x1, 0x2, 0x3, 0x4, 0x5];
    private static readonly byte[] _H264_Sps = [0x0, 0x0, 0x1, 0x7, 0xA, 0xB];
    private static readonly byte[] _H264_Sps4 = [0x0, 0x0, 0x0, 0x1, 0x7, 0xA, 0xB];
    private static readonly byte[] _H264_Pps = [0x0, 0x0, 0x1, 0x8, 0xA];
    private static readonly byte[] _H264_SpsPps = [.. _H264_Sps4, .. _H264_Pps];
    private static readonly byte[] _H264_Frame_IDR_05 = [0x0, 0x0, 0x1, 0x5, 0x8];
    private static readonly byte[] _H264_Frame_IDR_10 = [0x0, 0x0, 0x1, 0x5, 0xA, 0xB, 0xC, 0xD, 0xE, 0xF];
    private static readonly byte[] _H264_FrameNonIDR_05 = [0x0, 0x0, 0x1, 0x1, 0x9];

    private static readonly byte[] _H265_Vps = [0x0, 0x0, 0x1, 0x41, 0xA, 0xB];
    private static readonly byte[] _H265_Sps = [0x0, 0x0, 0x1, 0x43, 0xA, 0xB];
    private static readonly byte[] _H265_Pps = [0x0, 0x0, 0x1, 0x45, 0xA, 0xB];

    private readonly EncodedAccessUnitBuffer _Testee;

    public EncodedAccessUnitBufferTests()
    {
        _Testee = new EncodedAccessUnitBuffer(_Capacity, _MaxChunkSize);
    }

    [Fact]
    public void Fixture()
    {
        Assert.Equal(CodecType.Unknown, _Testee.Codec);
        Assert.False(_Testee.HasHeader);
        Assert.False(_Testee.Header.Any());
    }

    [Fact]
    public void Reset()
    {
        Add(_H264_SpsPps);
        Assert.True(_Testee.HasHeader);

        _Testee.Reset();

        Assert.Equal(CodecType.Unknown, _Testee.Codec);
        Assert.False(_Testee.HasHeader);
    }

    [Fact]
    public void Header_for_H264_complete_after_SPS_PPS()
    {
        Add(_H264_Sps);
        Add(_H264_Pps);

        Assert.Equal(CodecType.H264, _Testee.Codec);
        VerifyHeader(ParameterSetType.SPS, _H264_Sps);
        VerifyHeader(ParameterSetType.PPS, _H264_Pps);
    }

    [Fact]
    public void Header_for_H265_complete_after_VPS_SPS_PPS()
    {
        Add(_H265_Vps);
        Add(_H265_Sps);
        Add(_H265_Pps);

        Assert.Equal(CodecType.H265, _Testee.Codec);
        VerifyHeader(ParameterSetType.VPS, _H265_Vps);
        VerifyHeader(ParameterSetType.SPS, _H265_Sps);
        VerifyHeader(ParameterSetType.PPS, _H265_Pps);
    }

    [Fact]
    public void Mixed_header_and_VCL_are_stored()
    {
        // SPS + PPS + IDR slice in one chunk
        byte[] mixed = [.. _H264_Sps4, .. _H264_Pps, .. _H264_Frame_IDR_05];

        var nal = Add(mixed, 123);

        Assert.True(nal.HasValue);
        Assert.Equal(123, nal.Value.TimeNs);
        Assert.True(nal.Value.IsKeyFrame);
        Assert.Equal(0, nal.Value.Offset);
        Assert.Equal(mixed.Length, nal.Value.Size);

        VerifyHeader(ParameterSetType.SPS, _H264_Sps4);
        VerifyHeader(ParameterSetType.PPS, _H264_Pps);
        VerifyReplay(123, int.MaxValue, 123);
    }

    [Fact]
    public void Invalid_chunk_is_ignored()
    {
        var nal = Add(_Invalid);

        Assert.False(nal.HasValue);
        Assert.Equal(CodecType.Unknown, _Testee.Codec);
        Assert.False(_Testee.HasHeader);
    }

    [Fact]
    public void Header_chunk_is_not_stored()
    {
        var nal = Add(_H264_Sps);

        Assert.False(nal.HasValue);
    }

    [Fact]
    public void Frame_is_not_stored_if_header_is_not_complete()
    {
        Add(_H264_Sps);
        var nal = Add(_H264_Frame_IDR_05);

        Assert.False(nal.HasValue);
    }

    [Fact]
    public void Chunk_is_stored()
    {
        Add(_H264_SpsPps);
        var nal = Add(_H264_Frame_IDR_05, 42);

        Assert.True(nal.HasValue);
        Assert.Equal(42, nal.Value.TimeNs);
        Assert.True(nal.Value.IsKeyFrame);
        Assert.Equal(0, nal.Value.Offset);
        Assert.Equal(_H264_Frame_IDR_05.Length, nal.Value.Size);
        VerifyReplay(42, int.MaxValue, 42);
    }

    [Fact]
    public void Replay_is_empty_whitout_header()
    {
        Add(_H264_Sps);
        Add(_H264_Frame_IDR_05, 5);

        VerifyReplay(0, 10);
    }

    [Fact]
    public void Replay_is_empty_whitout_keyframe()
    {
        Add(_H264_SpsPps);
        Add(_H264_FrameNonIDR_05, 42);

        VerifyReplay(0, int.MaxValue);
    }

    [Fact]
    public void Replay_keyframe_and_frame()
    {
        Add(_H264_SpsPps);
        Add(_H264_Frame_IDR_05, 1);
        Add(_H264_FrameNonIDR_05, 2);

        bool found = _Testee.TryGetReplaySegment(1, 10, out var segment);
        Assert.True(found);
        Assert.Equal(2, segment.AccessUnits.Count);

        var entry = segment.AccessUnits[0];
        Assert.Equal(1, entry.TimeNs);
        Assert.True(entry.IsKeyFrame);
        Assert.Equal(_H264_Frame_IDR_05, entry.Buffer);

        entry = segment.AccessUnits[1];
        Assert.Equal(2, entry.TimeNs);
        Assert.False(entry.IsKeyFrame);
        Assert.Equal(_H264_FrameNonIDR_05, entry.Buffer);
    }

    [Fact]
    public void Replay_segment_copies_header_and_access_unit_buffers()
    {
        Add(_H264_SpsPps);
        Add(_H264_Frame_IDR_05, 1);
        Add(_H264_FrameNonIDR_05, 2);

        bool found = _Testee.TryGetReplaySegment(1, 10, out var segment);
        Assert.True(found);
        Assert.Equal(FoosVision.Ports.Media.EncodedReplayCodec.H264, segment.Codec);
        Assert.Equal(1, segment.StartTimeNs);
        Assert.Equal(2, segment.EndTimeNs);
        Assert.Equal(2, segment.ParameterSets.Count);
        Assert.Equal(2, segment.AccessUnits.Count);
        Assert.Equal(_H264_Frame_IDR_05, segment.AccessUnits[0].Buffer);
        Assert.Equal(_H264_FrameNonIDR_05, segment.AccessUnits[1].Buffer);
    }

    [Fact]
    public void Replay_uses_keyframe_at_or_before_requested_start()
    {
        Add(_H264_SpsPps);
        Add(_H264_Frame_IDR_05, 100);
        Add(_H264_FrameNonIDR_05, 150);
        Add(_H264_Frame_IDR_05, 200);
        Add(_H264_FrameNonIDR_05, 250);

        bool found = _Testee.TryGetReplaySegment(160, 250, out var segment);

        Assert.True(found);
        Assert.Equal(100, segment.StartTimeNs);
        Assert.Equal(250, segment.EndTimeNs);
        Assert.Equal([100, 150, 200, 250], segment.AccessUnits.Select(accessUnit => accessUnit.TimeNs));
    }

    [Fact]
    public void Snapshot_uses_actual_keyframe_aligned_buffer_range()
    {
        Add(_H264_SpsPps);
        Add(_H264_FrameNonIDR_05, 10);
        Add(_H264_Frame_IDR_05, 20);
        Add(_H264_FrameNonIDR_05, 30);

        bool found = _Testee.TryGetSnapshot(out var segment);

        Assert.True(found);
        Assert.Equal(20, segment.StartTimeNs);
        Assert.Equal(30, segment.EndTimeNs);
        Assert.Equal(2, segment.AccessUnits.Count);
        Assert.Equal(20, segment.AccessUnits[0].TimeNs);
        Assert.True(segment.AccessUnits[0].IsKeyFrame);
        Assert.Equal(30, segment.AccessUnits[1].TimeNs);
    }

    [Fact]
    public void Snapshot_is_empty_without_keyframe()
    {
        Add(_H264_SpsPps);
        Add(_H264_FrameNonIDR_05, 10);

        bool found = _Testee.TryGetSnapshot(out var segment);

        Assert.False(found);
        Assert.Empty(segment.AccessUnits);
        Assert.Equal(10, segment.StartTimeNs);
        Assert.Equal(10, segment.EndTimeNs);
    }

    [Fact]
    public void Replay_corner_cases()
    {
        Add(_H264_SpsPps);
        Add(_H264_FrameNonIDR_05, 1);
        Add(_H264_Frame_IDR_05, 2);
        Add(_H264_FrameNonIDR_05, 3);
        Add(_H264_Frame_IDR_05, 4);
        Add(_H264_FrameNonIDR_05, 5);

        VerifyReplay(2, 4, 2, 3, 4); // aligned
        VerifyReplay(1, 3); // no keyframe before start
        VerifyReplay(3, 5, 2, 3, 4, 5); // keyframe before start

        VerifyReplay(0, 2); // start outside left
        VerifyReplay(4, 6, 4, 5); // end outside
        VerifyReplay(0, 6); // start outside left

        VerifyReplay(3, 2); // end < start
        VerifyReplay(0, 0); // replay outside left
        VerifyReplay(6, 8); // replay outside right
    }

    [Fact]
    public void Replay_for_full_buffer()
    {
        Add(_H264_SpsPps);
        Add(_H264_Frame_IDR_05, 1);
        Add(_H264_Frame_IDR_05, 2);
        Add(_H264_Frame_IDR_10, 3);
        Add(_H264_Frame_IDR_10, 4);

        VerifyReplay(1, int.MaxValue, 1, 2, 3, 4);
    }

    [Fact]
    public void Replay_with_buffer_wrap_one_frame()
    {
        Add(_H264_SpsPps);
        Add(_H264_Frame_IDR_05, 1);
        Add(_H264_Frame_IDR_05, 2);
        Add(_H264_Frame_IDR_10, 3);
        Add(_H264_Frame_IDR_10, 4);
        Add(_H264_Frame_IDR_10, 5);
        Add(_H264_Frame_IDR_05, 6); // Replaces 1

        VerifyReplay(2, int.MaxValue, 2, 3, 4, 5, 6);
    }

    [Fact]
    public void Replay_with_buffer_wrap_two_frames()
    {
        Add(_H264_SpsPps);
        Add(_H264_Frame_IDR_05, 1);
        Add(_H264_Frame_IDR_05, 2);
        Add(_H264_Frame_IDR_10, 3);
        Add(_H264_Frame_IDR_10, 4);
        Add(_H264_Frame_IDR_10, 5);
        Add(_H264_Frame_IDR_10, 6); // Replaces 1 & 2

        VerifyReplay(3, int.MaxValue, 3, 4, 5, 6);
    }

    private EncodedAccessUnit? Add(byte[] chunk, int time = 0)
    {
        EncodedAccessUnit? nal = null;

        Array.Copy(chunk, 0, _Testee.Buffer, _Testee.Offset, chunk.Length);

        _Testee.SetEncodedUnitReadyNotificationSink((n) => nal = n);
        _Testee.Completed(time, chunk.Length);

        return nal;
    }

    private void VerifyHeader(ParameterSetType type, byte[] buffer)
    {
        Assert.True(_Testee.HasHeader);

        var entry = _Testee.Header.FirstOrDefault(h => h.Type == type);
        Assert.NotNull(entry);
        Assert.True(entry.Buffer.SequenceEqual(buffer));
    }

    private void VerifyReplay(int startTime, int endTime, params int[] expectedTimes)
    {
        bool found = _Testee.TryGetReplaySegment(startTime, endTime, out var segment);
        var expectedCount = expectedTimes.Length;

        if (expectedCount == 0)
        {
            Assert.False(found);
            Assert.Empty(segment.AccessUnits);
            return;
        }

        Assert.True(found);
        Assert.Equal(expectedCount, segment.AccessUnits.Count);

        for (int i = 0; i < expectedCount; i++)
        {
            Assert.Equal(expectedTimes[i], segment.AccessUnits[i].TimeNs);
        }
    }
}
