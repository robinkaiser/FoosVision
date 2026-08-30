// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Media.Windows.FileCapture;
using FoosVision.Media.Windows.FileCapture.Mp4;
using FoosVision.Ports.Media;

namespace FoosVision.Media.Windows.UnitTests.FileCapture;

public class FileCameraFeedTests
{
    [Fact]
    public async Task Start_returns_false_before_configure()
    {
        using FileCameraFeed feed = CreateFeed();

        bool result = await feed.Start(new FakeFrameSink(), new FakeEncodedAccessUnitSink());

        Assert.False(result);
    }

    [Fact]
    public async Task Configure_returns_false_when_stream_info_does_not_match_options()
    {
        FakeMp4AccessUnitSource source = new(new Mp4VideoStreamInfo(CodecType.H265, 1920, 1080), []);
        using FileCameraFeed feed = CreateFeed(source: source);

        bool result = await feed.Configure();

        Assert.False(result);
    }

    [Fact]
    public async Task Start_returns_false_when_decoder_configuration_fails()
    {
        FakeMp4AccessUnitSource source = new(new Mp4VideoStreamInfo(CodecType.H264, 1920, 1080), []);
        using FileCameraFeed feed = CreateFeed(source: source, decoderFactory: new ThrowingDecoderFactory());

        await feed.Configure();
        bool result = await feed.Start(new FakeFrameSink(), new FakeEncodedAccessUnitSink());

        Assert.False(result);
    }

    [Fact]
    public async Task Start_returns_false_when_already_running()
    {
        FakeMp4AccessUnitSource source = new(
            new Mp4VideoStreamInfo(CodecType.H264, 1920, 1080),
            [new Mp4AccessUnit(0, true, new byte[] { 1, 2, 3 })]);
        using FileCameraFeed feed = CreateFeed(source: source);
        await feed.Configure();

        bool firstStart = await feed.Start(new FakeFrameSink(), new FakeEncodedAccessUnitSink());
        bool secondStart = await feed.Start(new FakeFrameSink(), new FakeEncodedAccessUnitSink());
        await feed.Stop();

        Assert.True(firstStart);
        Assert.False(secondStart);
    }

    [Fact]
    public async Task Stop_is_idempotent()
    {
        FakeMp4AccessUnitSource source = new(new Mp4VideoStreamInfo(CodecType.H264, 1920, 1080), []);
        using FileCameraFeed feed = CreateFeed(source: source);
        await feed.Configure();

        await feed.Stop();
        await feed.Stop();
    }

    [Fact]
    public async Task Start_streams_encoded_units_and_decoded_frames_in_expected_order()
    {
        FakeMp4AccessUnitSource source = new(
            new Mp4VideoStreamInfo(CodecType.H264, 1920, 1080),
            [
                new Mp4AccessUnit(10, false, new byte[] { 0, 0, 1, 0x67 }),
                new Mp4AccessUnit(20, false, new byte[] { 0, 0, 1, 0x68 }),
                new Mp4AccessUnit(30, true, new byte[] { 0, 0, 1, 0x65 }),
                new Mp4AccessUnit(40, false, new byte[] { 0, 0, 1, 0x41 }),
            ]);
        FakeVideoDecoder decoder = new();
        FakeFrameSink frameSink = new();
        FakeEncodedAccessUnitSink encodedSink = new();
        using FileCameraFeed feed = CreateFeed(source: source, decoderFactory: new FakeDecoderFactory(decoder));

        await feed.Configure();
        bool started = await feed.Start(frameSink, encodedSink);
        await Task.Delay(80, TestContext.Current.CancellationToken);
        await feed.Stop();

        Assert.True(started);
        Assert.Equal(4, encodedSink.CompletedUnits.Count);
        Assert.Equal(10, encodedSink.CompletedUnits[0].TimestampNs);
        Assert.Equal([0, 0, 1, 0x67], encodedSink.CompletedUnits[0].Buffer);
        Assert.Equal([0, 0, 1, 0x41], encodedSink.CompletedUnits[3].Buffer);
        Assert.Single(frameSink.WrittenFrames);
        Assert.Equal(10, frameSink.WrittenFrames[0].TimestampNs);
    }

    [Fact]
    public async Task Start_drops_decoded_frame_when_frame_sink_is_exhausted()
    {
        FakeMp4AccessUnitSource source = new(
            new Mp4VideoStreamInfo(CodecType.H264, 1920, 1080),
            [new Mp4AccessUnit(10, true, new byte[] { 0, 0, 1, 0x65 })]);
        using FileCameraFeed feed = CreateFeed(source: source, decoderFactory: new FakeDecoderFactory(new FakeVideoDecoder()));

        await feed.Configure();
        bool started = await feed.Start(new ExhaustedFrameSink(), new FakeEncodedAccessUnitSink());
        await Task.Delay(80, TestContext.Current.CancellationToken);
        await feed.Stop();

        Assert.True(started);
    }

    [Fact]
    public async Task Configure_and_start_forward_decoder_options()
    {
        FakeMp4AccessUnitSource source = new(new Mp4VideoStreamInfo(CodecType.H265, 1280, 720), []);
        FakeVideoDecoder decoder = new();
        using FileCameraFeed feed = new(
            new FileCameraFeedOptions(
                "clip.mp4",
                CodecType.H265,
                1280,
                720,
                HardwareMode: WindowsVideoDecoderHardwareMode.RequireHardware),
            new FakeMp4AccessUnitSourceFactory(source),
            new FakeDecoderFactory(decoder));

        bool configured = await feed.Configure();
        bool started = await feed.Start(new FakeFrameSink(), new FakeEncodedAccessUnitSink());
        await feed.Stop();

        Assert.True(configured);
        Assert.True(started);
        Assert.NotNull(decoder.ConfiguredOptions);
        Assert.Equal(WindowsVideoDecoderHardwareMode.RequireHardware, decoder.ConfiguredOptions!.HardwareMode);
        Assert.Equal(CodecType.H265, decoder.ConfiguredOptions.Codec);
    }

    [Fact]
    public async Task PlaybackCompleted_is_raised_when_stream_reaches_end()
    {
        FakeMp4AccessUnitSource source = new(
            new Mp4VideoStreamInfo(CodecType.H264, 1920, 1080),
            [new Mp4AccessUnit(10, true, new byte[] { 0, 0, 1, 0x65 })]);
        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using FileCameraFeed feed = CreateFeed(source: source, decoderFactory: new FakeDecoderFactory(new FakeVideoDecoder()));
        feed.PlaybackCompleted += () => completion.TrySetResult();

        await feed.Configure();
        bool started = await feed.Start(new FakeFrameSink(), new FakeEncodedAccessUnitSink());

        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        await feed.Stop();

        Assert.True(started);
    }

    private static FileCameraFeed CreateFeed(FakeMp4AccessUnitSource? source = null, IWindowsVideoDecoderFactory? decoderFactory = null)
    {
        source ??= new FakeMp4AccessUnitSource(new Mp4VideoStreamInfo(CodecType.H264, 1920, 1080), []);
        decoderFactory ??= new FakeDecoderFactory(new FakeVideoDecoder());
        return new FileCameraFeed(
            new FileCameraFeedOptions("clip.mp4", CodecType.H264, 1920, 1080),
            new FakeMp4AccessUnitSourceFactory(source),
            decoderFactory);
    }

    private record CompletedEncodedUnit(long TimestampNs, byte[] Buffer);

    private record WrittenFrame(long TimestampNs, byte[] Buffer);

    private class FakeMp4AccessUnitSourceFactory(FakeMp4AccessUnitSource source) : IMp4AccessUnitSourceFactory
    {
        public IMp4AccessUnitSource Create() => source;
    }

    private class FakeMp4AccessUnitSource(Mp4VideoStreamInfo streamInfo, IReadOnlyList<Mp4AccessUnit> units) : IMp4AccessUnitSource
    {
        private int _Index;

        public Mp4VideoStreamInfo StreamInfo { get; } = streamInfo;

        public void Configure(string filePath)
        {
        }

        public void Reset()
        {
            _Index = 0;
        }

        public bool TryReadNextAccessUnit([NotNullWhen(true)] out Mp4AccessUnit? accessUnit)
        {
            if (_Index >= units.Count)
            {
                accessUnit = null;
                return false;
            }

            accessUnit = units[_Index];
            _Index++;
            return true;
        }

        public void Dispose()
        {
        }
    }

    private class FakeDecoderFactory(IWindowsVideoDecoder decoder) : IWindowsVideoDecoderFactory
    {
        public IWindowsVideoDecoder Create() => decoder;
    }

    private class ThrowingDecoderFactory : IWindowsVideoDecoderFactory
    {
        public IWindowsVideoDecoder Create() => new ThrowingVideoDecoder();
    }

    private class FakeVideoDecoder : IWindowsVideoDecoder
    {
        private readonly Queue<WindowsDecodedFrame> _Frames = [];

        public bool IsConfigured { get; private set; }

        public bool IsHardwareAccelerated => false;

        public WindowsVideoDecoderOptions? Options => ConfiguredOptions;

        public WindowsVideoDecoderOptions? ConfiguredOptions { get; private set; }

        public void Configure(WindowsVideoDecoderOptions options)
        {
            IsConfigured = true;
            ConfiguredOptions = options;
        }

        public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool queueDecodedFrames = true)
        {
            if (queueDecodedFrames)
            {
                _Frames.Enqueue(new WindowsDecodedFrame(timeNs, 2, 2, 2, FrameByteFormat.RGBA8888, [1, 2, 3, 4], 4));
            }
        }

        public bool TryDequeueFrame([NotNullWhen(true)] out WindowsDecodedFrame? frame)
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
        }

        public void Reset()
        {
            while (_Frames.Count != 0)
            {
                _Frames.Dequeue().Dispose();
            }
        }

        public void Dispose()
        {
            Reset();
        }
    }

    private class ThrowingVideoDecoder : IWindowsVideoDecoder
    {
        public bool IsConfigured => false;

        public bool IsHardwareAccelerated => false;

        public WindowsVideoDecoderOptions? Options => null;

        public void Configure(WindowsVideoDecoderOptions options)
        {
            throw new InvalidOperationException("Decoder setup failed.");
        }

        public void PushAccessUnit(ReadOnlySpan<byte> buffer, long timeNs, bool isKeyFrame, bool queueDecodedFrames = true)
        {
        }

        public bool TryDequeueFrame([NotNullWhen(true)] out WindowsDecodedFrame? frame)
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

    private class FakeFrameSink : IFrameSink
    {
        public List<WrittenFrame> WrittenFrames { get; } = [];

        public IProducerFrameHandle AcquireForWrite()
        {
            return new FakeProducerFrameHandle(WrittenFrames);
        }
    }

    private class ExhaustedFrameSink : IFrameSink
    {
        public IProducerFrameHandle AcquireForWrite()
        {
            return NullProducerFrameHandle.Instance;
        }
    }

    private class FakeProducerFrameHandle(List<WrittenFrame> writtenFrames) : IProducerFrameHandle
    {
        public byte[] BufferRGBA8888 { get; } = new byte[4];

        public void MarkWritten(long timestampNs)
        {
            writtenFrames.Add(new WrittenFrame(timestampNs, BufferRGBA8888.ToArray()));
        }
    }

    private class FakeEncodedAccessUnitSink : IEncodedAccessUnitSink
    {
        public List<CompletedEncodedUnit> CompletedUnits { get; } = [];

        public byte[] Buffer { get; } = new byte[1024];

        public int Offset => 0;

        public void Completed(long timestampNs, int size)
        {
            CompletedUnits.Add(new CompletedEncodedUnit(timestampNs, Buffer[..size].ToArray()));
        }
    }
}
