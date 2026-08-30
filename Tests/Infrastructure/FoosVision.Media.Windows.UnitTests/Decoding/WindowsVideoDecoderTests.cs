// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Media.Windows.Decoding.Ffmpeg;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Windows.UnitTests.Decoding;

public class WindowsVideoDecoderTests
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
    public void Configure_falls_back_to_software_when_preferred_hardware_configuration_fails()
    {
        SequencedSessionFactory sessionFactory = new(
            [
                new ThrowingSession(),
                new FakeSession(),
            ]);
        using WindowsVideoDecoder decoder = new(sessionFactory);

        decoder.Configure(new WindowsVideoDecoderOptions(
            CodecType.H264,
            1920,
            1080,
            HardwareMode: WindowsVideoDecoderHardwareMode.PreferHardware));

        Assert.Equal(2, sessionFactory.CreateCalls.Count);
        Assert.Equal(WindowsVideoDecoderHardwareMode.PreferHardware, sessionFactory.CreateCalls[0].HardwareMode);
        Assert.Equal(WindowsVideoDecoderHardwareMode.SoftwareOnly, sessionFactory.CreateCalls[1].HardwareMode);
        Assert.False(decoder.IsHardwareAccelerated);
    }

    [Fact]
    public void Configure_does_not_fall_back_when_hardware_is_required()
    {
        SequencedSessionFactory sessionFactory = new([new ThrowingSession()]);
        using WindowsVideoDecoder decoder = new(sessionFactory);

        Assert.Throws<InvalidOperationException>(() => decoder.Configure(new WindowsVideoDecoderOptions(
            CodecType.H264,
            1920,
            1080,
            HardwareMode: WindowsVideoDecoderHardwareMode.RequireHardware)));
        Assert.Single(sessionFactory.CreateCalls);
    }

    [Fact]
    public void Configure_exposes_hardware_acceleration_state_of_active_session()
    {
        using WindowsVideoDecoder decoder = new(new FakeHardwareSessionFactory(new HardwareFakeSession()));

        decoder.Configure(new WindowsVideoDecoderOptions(
            CodecType.H264,
            1920,
            1080,
            HardwareMode: WindowsVideoDecoderHardwareMode.RequireHardware));

        Assert.True(decoder.IsHardwareAccelerated);
    }

    [Fact]
    public void PushAccessUnit_requires_configuration()
    {
        using WindowsVideoDecoder decoder = new(new FakeSessionFactory(new FakeSession()));

        Assert.Throws<InvalidOperationException>(() => decoder.PushAccessUnit(_H264Idr, 1, true));
    }

    [Fact]
    public void H264_vcl_before_parameter_sets_is_ignored()
    {
        FakeSession session = new();
        using WindowsVideoDecoder decoder = CreateDecoder(session, CodecType.H264);

        decoder.PushAccessUnit(_H264Idr, 10, true);

        Assert.Empty(session.SubmittedAccessUnits);
    }

    [Fact]
    public void H264_keyframe_pushes_parameter_sets_before_access_unit()
    {
        FakeSession session = new();
        using WindowsVideoDecoder decoder = CreateDecoder(session, CodecType.H264);

        decoder.PushAccessUnit(_H264Sps, 1, false);
        decoder.PushAccessUnit(_H264Pps, 2, false);
        decoder.PushAccessUnit(_H264Idr, 3, true);

        Assert.Equal(3, session.SubmittedAccessUnits.Count);
        Assert.True(session.SubmittedAccessUnits[0].SequenceEqual(_H264Sps));
        Assert.True(session.SubmittedAccessUnits[1].SequenceEqual(_H264Pps));
        Assert.True(session.SubmittedAccessUnits[2].SequenceEqual(_H264Idr));
    }

    [Fact]
    public void H265_keyframe_requires_vps_sps_and_pps()
    {
        FakeSession session = new();
        using WindowsVideoDecoder decoder = CreateDecoder(session, CodecType.H265);

        decoder.PushAccessUnit(_H265Sps, 1, false);
        decoder.PushAccessUnit(_H265Pps, 2, false);
        decoder.PushAccessUnit(_H265Idr, 3, true);

        Assert.Empty(session.SubmittedAccessUnits);
    }

    [Fact]
    public void H265_keyframe_pushes_all_parameter_sets_before_access_unit()
    {
        FakeSession session = new();
        using WindowsVideoDecoder decoder = CreateDecoder(session, CodecType.H265);

        decoder.PushAccessUnit(_H265Vps, 1, false);
        decoder.PushAccessUnit(_H265Sps, 2, false);
        decoder.PushAccessUnit(_H265Pps, 3, false);
        decoder.PushAccessUnit(_H265Idr, 4, true);

        Assert.Equal(4, session.SubmittedAccessUnits.Count);
        Assert.True(session.SubmittedAccessUnits[0].SequenceEqual(_H265Vps));
        Assert.True(session.SubmittedAccessUnits[1].SequenceEqual(_H265Sps));
        Assert.True(session.SubmittedAccessUnits[2].SequenceEqual(_H265Pps));
        Assert.True(session.SubmittedAccessUnits[3].SequenceEqual(_H265Idr));
    }

    [Fact]
    public void TryDequeueFrame_returns_frames_produced_by_session()
    {
        FakeSession session = new();
        session.EnqueueFrame(new FfmpegDecodedFrame(77, 4, 3, 16, FrameByteFormat.RGBA8888, new byte[48], 48, false));

        using WindowsVideoDecoder decoder = CreateDecoder(session, CodecType.H264);
        decoder.PushAccessUnit(_H264Sps, 1, false);
        decoder.PushAccessUnit(_H264Pps, 2, false);
        decoder.PushAccessUnit(_H264Idr, 77, true);

        bool hasFrame = decoder.TryDequeueFrame(out WindowsDecodedFrame? frame);

        Assert.True(hasFrame);
        Assert.NotNull(frame);
        Assert.Equal(77, frame.TimeNs);
        Assert.Equal(4, frame.Width);
        Assert.Equal(3, frame.Height);
    }

    [Fact]
    public void Flush_and_reset_are_forwarded_to_session()
    {
        FakeSession session = new();
        using WindowsVideoDecoder decoder = CreateDecoder(session, CodecType.H264);

        decoder.Flush();
        decoder.Reset();

        Assert.Equal(1, session.FlushCount);
        Assert.Equal(1, session.ResetCount);
    }

    [Fact]
    public void Decoder_can_be_reused_after_reset()
    {
        FakeSession session = new();
        using WindowsVideoDecoder decoder = CreateDecoder(session, CodecType.H264);

        decoder.PushAccessUnit(_H264Sps, 1, false);
        decoder.PushAccessUnit(_H264Pps, 2, false);
        decoder.Reset();
        decoder.PushAccessUnit(_H264NonIdr, 3, false);
        decoder.PushAccessUnit(_H264Sps, 4, false);
        decoder.PushAccessUnit(_H264Pps, 5, false);
        decoder.PushAccessUnit(_H264Idr, 6, true);

        Assert.Equal(3, session.SubmittedAccessUnits.Count);
        Assert.True(session.SubmittedAccessUnits[2].SequenceEqual(_H264Idr));
    }

    private static WindowsVideoDecoder CreateDecoder(FakeSession session, CodecType codec)
    {
        WindowsVideoDecoder decoder = new(new FakeSessionFactory(session));
        decoder.Configure(new WindowsVideoDecoderOptions(codec, 1920, 1080));
        return decoder;
    }

    private class FakeHardwareSessionFactory(HardwareFakeSession session) : IFfmpegDecoderSessionFactory
    {
        public IFfmpegDecoderSession Create(FfmpegDecoderOptions options) => session;
    }

    private class FakeSessionFactory(FakeSession session) : IFfmpegDecoderSessionFactory
    {
        public IFfmpegDecoderSession Create(FfmpegDecoderOptions options) => session;
    }

    private class SequencedSessionFactory(IEnumerable<IFfmpegDecoderSession> sessions) : IFfmpegDecoderSessionFactory
    {
        private readonly Queue<IFfmpegDecoderSession> _Sessions = new(sessions);

        public List<FfmpegDecoderOptions> CreateCalls { get; } = [];

        public IFfmpegDecoderSession Create(FfmpegDecoderOptions options)
        {
            CreateCalls.Add(options);
            return _Sessions.Dequeue();
        }
    }

    private class HardwareFakeSession : IFfmpegDecoderSession
    {
        public bool IsConfigured { get; private set; }

        public bool IsHardwareAccelerated => true;

        public void Configure(FfmpegDecoderOptions options)
        {
            IsConfigured = true;
        }

        public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool enqueueFrames = true)
        {
        }

        public bool TryDequeueFrame([NotNullWhen(true)] out FfmpegDecodedFrame? frame)
        {
            frame = null;
            return false;
        }

        public void Flush()
        {
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }

    private class ThrowingSession : IFfmpegDecoderSession
    {
        public bool IsConfigured => false;

        public bool IsHardwareAccelerated => true;

        public void Configure(FfmpegDecoderOptions options)
        {
            throw new InvalidOperationException("Hardware decoder setup failed.");
        }

        public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool enqueueFrames = true)
        {
        }

        public bool TryDequeueFrame([NotNullWhen(true)] out FfmpegDecodedFrame? frame)
        {
            frame = null;
            return false;
        }

        public void Flush()
        {
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }

    private class FakeSession : IFfmpegDecoderSession
    {
        private readonly Queue<FfmpegDecodedFrame> _Frames = [];

        public List<byte[]> SubmittedAccessUnits { get; } = [];

        public int FlushCount { get; private set; }

        public int ResetCount { get; private set; }

        public bool IsConfigured { get; private set; }

        public bool IsHardwareAccelerated => false;

        public void Configure(FfmpegDecoderOptions options)
        {
            IsConfigured = true;
        }

        public void EnqueueFrame(FfmpegDecodedFrame frame)
        {
            _Frames.Enqueue(frame);
        }

        public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool enqueueFrames = true)
        {
            SubmittedAccessUnits.Add(buffer.ToArray());
        }

        public bool TryDequeueFrame([NotNullWhen(true)] out FfmpegDecodedFrame? frame)
        {
            if (_Frames.Count == 0)
            {
                frame = null;
                return false;
            }

            frame = _Frames.Dequeue();
            return true;
        }

        public void Flush()
        {
            FlushCount++;
        }

        public void Reset()
        {
            ResetCount++;
        }

        public void Dispose()
        {
            while (_Frames.Count != 0)
            {
                _Frames.Dequeue().Dispose();
            }
        }
    }
}
