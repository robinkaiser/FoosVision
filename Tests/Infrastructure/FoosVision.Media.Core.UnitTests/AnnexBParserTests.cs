// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo.AnnexB;

namespace FoosVision.Media.Core.UnitTests;

public class AnnexBParserTests
{
    [Fact]
    public void TryFindStartCode_returns_false_for_buffer_without_start_code()
    {
        byte[] buffer = [0x00, 0x00, 0x00, 0x02];

        bool hasStartCode = AnnexBParser.TryFindStartCode(buffer, 0, 4, out _, out _);

        Assert.False(hasStartCode);
    }

    [Fact]
    public void TryFindStartCode_detects_three_byte_start_code()
    {
        byte[] buffer = [0x00, 0x00, 0x01, 0x67, 0xAA];

        bool hasStartCode = AnnexBParser.TryFindStartCode(buffer, 0, 5, out int startCodeOffset, out int headerOffset);

        Assert.True(hasStartCode);
        Assert.Equal(0, startCodeOffset);
        Assert.Equal(3, headerOffset);
    }

    [Fact]
    public void TryFindStartCode_detects_four_byte_start_code()
    {
        byte[] buffer = [0x00, 0x00, 0x00, 0x01, 0x67, 0xAA];

        bool hasStartCode = AnnexBParser.TryFindStartCode(buffer, 0, 6, out int startCodeOffset, out int headerOffset);

        Assert.True(hasStartCode);
        Assert.Equal(0, startCodeOffset);
        Assert.Equal(4, headerOffset);
    }

    [Fact]
    public void TryFindStartCode_respects_search_offset()
    {
        byte[] buffer = [0xFF, 0xFF, 0xFF, 0x00, 0x00, 0x01, 0x67, 0xAA];

        bool hasStartCode = AnnexBParser.TryFindStartCode(buffer, 3, 8, out int startCodeOffset, out int headerOffset);

        Assert.True(hasStartCode);
        Assert.Equal(3, startCodeOffset);
        Assert.Equal(6, headerOffset);
    }

    [Fact]
    public void TryFindStartCode_search_window_to_narrow()
    {
        byte[] buffer = [0x00, 0x00, 0x01];

        bool hasStartCode = AnnexBParser.TryFindStartCode(buffer, 0, 3, out _, out _);

        Assert.False(hasStartCode);
    }

    [Fact]
    public void FindNals_returns_empty_for_invalid_range()
    {
        byte[] buffer = [0x00, 0x00, 0x00, 0x01, 0x67, 0xAA];
        var nals = new AnnexBNalRange[1];

        var count = AnnexBParser.FindNals(buffer, 5, 4, nals, 1);

        Assert.Equal(0, count);
    }

    [Fact]
    public void FindNals_returns_single_nal_for_single_access_unit()
    {
        byte[] buffer = [0x00, 0x00, 0x00, 0x01, 0x67, 0xAA, 0xBB];
        var nals = new AnnexBNalRange[1];

        var count = AnnexBParser.FindNals(buffer, 0, 7, nals, 1);

        Assert.Equal(1, count);

        Assert.Equal(0, nals[0].StartOffset);
        Assert.Equal(4, nals[0].HeaderOffset);
        Assert.Equal(7, nals[0].EndOffsetExclusive);
    }

    [Fact]
    public void FindNals_returns_two_nals_for_mixed_three_and_four_byte_start_codes()
    {
        byte[] buffer =
        [
            0x00, 0x00, 0x00, 0x01, 0x67, 0xAA,
            0x00, 0x00, 0x01, 0x68, 0xBB
        ];
        var nals = new AnnexBNalRange[2];

        var count = AnnexBParser.FindNals(buffer, 0, 11, nals, 2);

        Assert.Equal(2, count);

        Assert.Equal(0, nals[0].StartOffset);
        Assert.Equal(4, nals[0].HeaderOffset);
        Assert.Equal(6, nals[0].EndOffsetExclusive);

        Assert.Equal(6, nals[1].StartOffset);
        Assert.Equal(9, nals[1].HeaderOffset);
        Assert.Equal(11, nals[1].EndOffsetExclusive);
    }

    [Fact]
    public void FindNals_uses_end_offset_exclusive()
    {
        byte[] buffer =
        [
            0x00, 0x00, 0x00, 0x01, 0x67, 0xAA,
            0x00, 0x00, 0x01, 0x68, 0xBB
        ];
        var nals = new AnnexBNalRange[2];

        var count = AnnexBParser.FindNals(buffer, 0, 6, nals, 2);

        Assert.Equal(1, count);

        Assert.Equal(0, nals[0].StartOffset);
        Assert.Equal(4, nals[0].HeaderOffset);
        Assert.Equal(6, nals[0].EndOffsetExclusive);
    }

    [Fact]
    public void FindNals_ignores_leading_bytes_before_first_start_code()
    {
        byte[] buffer = [0xFF, 0xEE, 0xDD, 0x00, 0x00, 0x01, 0x67, 0xAA];
        var nals = new AnnexBNalRange[1];

        var count = AnnexBParser.FindNals(buffer, 0, 8, nals, 1);

        Assert.Equal(1, count);

        Assert.Equal(3, nals[0].StartOffset);
        Assert.Equal(6, nals[0].HeaderOffset);
        Assert.Equal(8, nals[0].EndOffsetExclusive);
    }

    [Fact]
    public void FindNals_returns_last_nal_until_end_of_range()
    {
        byte[] buffer =
        [
            0x00, 0x00, 0x00, 0x01, 0x67, 0xAA,
            0x00, 0x00, 0x01, 0x68, 0xBB, 0xCC
        ];
        var nals = new AnnexBNalRange[2];

        var count = AnnexBParser.FindNals(buffer, 0, 12, nals, 2);

        Assert.Equal(2, count);

        Assert.Equal(0, nals[0].StartOffset);
        Assert.Equal(4, nals[0].HeaderOffset);
        Assert.Equal(6, nals[0].EndOffsetExclusive);

        Assert.Equal(6, nals[1].StartOffset);
        Assert.Equal(9, nals[1].HeaderOffset);
        Assert.Equal(12, nals[1].EndOffsetExclusive);
    }

    [Fact]
    public void FindNals_signals_nal_overflow()
    {
        byte[] buffer =
        [
            0x00, 0x00, 0x00, 0x01, 0x67, 0xAA,
            0x00, 0x00, 0x01, 0x68, 0xBB, 0xCC
        ];
        var nals = new AnnexBNalRange[1];

        var count = AnnexBParser.FindNals(buffer, 0, 12, nals, 1);

        Assert.Equal(-1, count);
    }
}
