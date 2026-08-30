// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;

namespace FoosVision.Media.Core.UnitTests;

public class AccessUnitPreprocessorTests
{
    private static readonly byte[] _H264Sps = [0x0, 0x0, 0x1, 0x7, 0x64, 0x00];
    private static readonly byte[] _H264Pps = [0x0, 0x0, 0x1, 0x8, 0xEE, 0x06];
    private static readonly byte[] _H264Idr = [0x0, 0x0, 0x1, 0x5, 0x88, 0x84];
    private static readonly byte[] _H264NonIdr = [0x0, 0x0, 0x1, 0x1, 0x77, 0x55];
    private static readonly byte[] _H265Vps = [0x0, 0x0, 0x1, 0x41, 0xAA, 0xBB];
    private static readonly byte[] _H265Sps = [0x0, 0x0, 0x1, 0x43, 0xAA, 0xBB];
    private static readonly byte[] _H265Pps = [0x0, 0x0, 0x1, 0x45, 0xAA, 0xBB];
    private static readonly byte[] _H265Idr = [0x0, 0x0, 0x1, 0x27, 0x99, 0x88];

    [Fact]
    public void H264_vcl_before_parameter_sets_is_ignored()
    {
        AccessUnitPreprocessor preprocessor = new();
        List<AccessUnitDispatch> dispatches = [];

        bool prepared = preprocessor.TryPrepare(_H264Idr, CodecType.H264, 10, isKeyFrame: true, queueDecodedFrames: true, dispatches);

        Assert.False(prepared);
        Assert.Empty(dispatches);
    }

    [Fact]
    public void H264_keyframe_pushes_parameter_sets_before_access_unit()
    {
        AccessUnitPreprocessor preprocessor = new();
        List<AccessUnitDispatch> dispatches = [];

        preprocessor.TryPrepare(_H264Sps, CodecType.H264, 1, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        preprocessor.TryPrepare(_H264Pps, CodecType.H264, 2, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        bool prepared = preprocessor.TryPrepare(_H264Idr, CodecType.H264, 3, isKeyFrame: true, queueDecodedFrames: true, dispatches);

        Assert.True(prepared);
        Assert.Equal(3, dispatches.Count);
        Assert.True(dispatches[0].Buffer.Span.SequenceEqual(_H264Sps));
        Assert.True(dispatches[1].Buffer.Span.SequenceEqual(_H264Pps));
        Assert.True(dispatches[2].Buffer.Span.SequenceEqual(_H264Idr));
    }

    [Fact]
    public void H265_keyframe_requires_vps_sps_and_pps()
    {
        AccessUnitPreprocessor preprocessor = new();
        List<AccessUnitDispatch> dispatches = [];

        preprocessor.TryPrepare(_H265Sps, CodecType.H265, 1, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        preprocessor.TryPrepare(_H265Pps, CodecType.H265, 2, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        bool prepared = preprocessor.TryPrepare(_H265Idr, CodecType.H265, 3, isKeyFrame: true, queueDecodedFrames: true, dispatches);

        Assert.False(prepared);
        Assert.Empty(dispatches);
    }

    [Fact]
    public void H265_keyframe_pushes_all_parameter_sets_before_access_unit()
    {
        AccessUnitPreprocessor preprocessor = new();
        List<AccessUnitDispatch> dispatches = [];

        preprocessor.TryPrepare(_H265Vps, CodecType.H265, 1, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        preprocessor.TryPrepare(_H265Sps, CodecType.H265, 2, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        preprocessor.TryPrepare(_H265Pps, CodecType.H265, 3, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        bool prepared = preprocessor.TryPrepare(_H265Idr, CodecType.H265, 4, isKeyFrame: true, queueDecodedFrames: true, dispatches);

        Assert.True(prepared);
        Assert.Equal(4, dispatches.Count);
        Assert.True(dispatches[0].Buffer.Span.SequenceEqual(_H265Vps));
        Assert.True(dispatches[1].Buffer.Span.SequenceEqual(_H265Sps));
        Assert.True(dispatches[2].Buffer.Span.SequenceEqual(_H265Pps));
        Assert.True(dispatches[3].Buffer.Span.SequenceEqual(_H265Idr));
    }

    [Fact]
    public void Reset_requires_parameter_sets_again()
    {
        AccessUnitPreprocessor preprocessor = new();
        List<AccessUnitDispatch> dispatches = [];

        preprocessor.TryPrepare(_H264Sps, CodecType.H264, 1, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        preprocessor.TryPrepare(_H264Pps, CodecType.H264, 2, isKeyFrame: false, queueDecodedFrames: false, dispatches);
        preprocessor.Reset();

        bool prepared = preprocessor.TryPrepare(_H264NonIdr, CodecType.H264, 3, isKeyFrame: false, queueDecodedFrames: true, dispatches);

        Assert.False(prepared);
        Assert.Empty(dispatches);
    }
}
