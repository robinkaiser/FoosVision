// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Media.Windows.FileCapture;

namespace FoosVision.Media.Windows.IntegrationTests.FileCapture;

public class FileCameraFeedIntegrationTests
{
    private static readonly SampleVideo _H264Sample = new(
        FilePath: @"D:\Projects\FoosVision.Integration\FileCapture\H.264.mp4",
        Codec: CodecType.H264,
        Width: 1920,
        Height: 1080);

    private static readonly SampleVideo _H265Sample = new(
        FilePath: @"D:\Projects\FoosVision.Integration\FileCapture\H.265.mp4",
        Codec: CodecType.H265,
        Width: 1920,
        Height: 1080);

    [Fact]
    public async Task H264_mp4_produces_encoded_units_and_decoded_frames()
    {
        await VerifySampleAsync(_H264Sample, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task H265_mp4_produces_encoded_units_and_decoded_frames()
    {
        await VerifySampleAsync(_H265Sample, TestContext.Current.CancellationToken);
    }

    private static async Task VerifySampleAsync(SampleVideo sample, CancellationToken cancellationToken)
    {
        Assert.SkipWhen(LocalIntegrationData.ShouldSkipFile(sample.FilePath), $"Local integration sample file '{sample.FilePath}' is not available in public test runs.");

        FfmpegIntegrationBootstrap.EnsureInitialized();

        CollectingFrameSink frameSink = new(sample.Width * sample.Height * 4);
        CollectingEncodedAccessUnitSink encodedSink = new(4 * 1024 * 1024);

        using FileCameraFeed feed = new(new FileCameraFeedOptions(
            sample.FilePath,
            sample.Codec,
            sample.Width,
            sample.Height,
            HardwareMode: WindowsVideoDecoderHardwareMode.RequireHardware));

        bool configured = await feed.Configure();
        Assert.True(configured);

        bool started = await feed.Start(frameSink, encodedSink);
        Assert.True(started);

        bool encodedReady = await WaitUntilAsync(() => encodedSink.CompletedUnits.Count > 0, TimeSpan.FromSeconds(5), cancellationToken);
        bool frameReady = await WaitUntilAsync(() => frameSink.WrittenFrames.Count > 0, TimeSpan.FromSeconds(5), cancellationToken);

        Assert.True(feed.IsHardwareDecodingActive, "Expected hardware decoding to be active.");

        await feed.Stop();

        Assert.True(encodedReady, "Expected at least one encoded access unit to be emitted.");
        Assert.True(frameReady, "Expected at least one decoded frame to be emitted.");
        Assert.True(encodedSink.CompletedUnits[0].Size > 0);
        Assert.True(frameSink.WrittenFrames[0].TimestampNs > 0, "Expected decoded frame timestamps to propagate from the MP4 access units.");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (condition())
            {
                return true;
            }

            await Task.Delay(20, cancellationToken);
        }

        return condition();
    }

    private record SampleVideo(string FilePath, CodecType Codec, int Width, int Height);

    private record CompletedEncodedAccessUnit(long TimestampNs, int Size);

    private record WrittenFrame(long TimestampNs, int Size);

    private class CollectingFrameSink(int bufferSize) : IFrameSink
    {
        public List<WrittenFrame> WrittenFrames { get; } = [];

        public IProducerFrameHandle AcquireForWrite()
        {
            return new CollectingProducerFrameHandle(bufferSize, WrittenFrames);
        }
    }

    private class CollectingProducerFrameHandle(int bufferSize, List<WrittenFrame> writtenFrames) : IProducerFrameHandle
    {
        public byte[] BufferRGBA8888 { get; } = new byte[bufferSize];

        public void MarkWritten(long timestampNs)
        {
            writtenFrames.Add(new WrittenFrame(timestampNs, BufferRGBA8888!.Length));
        }
    }

    private class CollectingEncodedAccessUnitSink(int capacity) : IEncodedAccessUnitSink
    {
        public List<CompletedEncodedAccessUnit> CompletedUnits { get; } = [];

        public byte[] Buffer { get; } = new byte[capacity];

        public int Offset => 0;

        public void Completed(long timestampNs, int size)
        {
            CompletedUnits.Add(new CompletedEncodedAccessUnit(timestampNs, size));
        }
    }
}
