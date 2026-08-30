// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.Media.Windows.FileCapture.Mp4;

namespace FoosVision.Media.Windows.IntegrationTests.Decoding;

public class WindowsVideoDecoderIntegrationTests
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

    private readonly ITestOutputHelper _Output;

    public WindowsVideoDecoderIntegrationTests(ITestOutputHelper output)
    {
        _Output = output;
    }

    [Fact]
    public void H264_mp4_decodes_all_frames()
    {
        DecodeWholeFile(_H264Sample);
    }

    [Fact]
    public void H265_mp4_decodes_all_frames()
    {
        DecodeWholeFile(_H265Sample);
    }

    [Fact]
    public void H264_mp4_decodes_and_materializes_only_every_fourth_frame()
    {
        DecodeWholeFile(_H264Sample, queueEveryNthFrame: 4);
    }

    private void DecodeWholeFile(SampleVideo sample, int queueEveryNthFrame = 1)
    {
        Assert.SkipWhen(LocalIntegrationData.ShouldSkipFile(sample.FilePath), $"Local integration sample file '{sample.FilePath}' is not available in public test runs.");

        FfmpegIntegrationBootstrap.EnsureInitialized();
        Assert.True(queueEveryNthFrame >= 1, "queueEveryNthFrame must be at least 1.");

        using FfmpegMp4AccessUnitSource accessUnitSource = new();
        accessUnitSource.Configure(sample.FilePath);

        Assert.Equal(sample.Codec, accessUnitSource.StreamInfo.Codec);
        Assert.Equal(sample.Width, accessUnitSource.StreamInfo.Width);
        Assert.Equal(sample.Height, accessUnitSource.StreamInfo.Height);

        using WindowsVideoDecoder decoder = new();
        decoder.Configure(new WindowsVideoDecoderOptions(
            sample.Codec,
            sample.Width,
            sample.Height,
            HardwareMode: WindowsVideoDecoderHardwareMode.RequireHardware));

        long startTime = Stopwatch.GetTimestamp();
        int accessUnitCount = 0;
        int decodedFrameCount = 0;

        while (accessUnitSource.TryReadNextAccessUnit(out Mp4AccessUnit? accessUnit))
        {
            bool queueDecodedFrames = accessUnitCount % queueEveryNthFrame == 0;
            accessUnitCount++;
            decoder.PushAccessUnit(accessUnit.Buffer.Span, accessUnit.TimestampNs, accessUnit.IsKeyFrame, queueDecodedFrames);
            decodedFrameCount += DrainDecodedFrames(decoder);
        }

        decoder.Flush();
        decodedFrameCount += DrainDecodedFrames(decoder);
        TimeSpan elapsedTime = Stopwatch.GetElapsedTime(startTime);

        Assert.True(decoder.IsHardwareAccelerated, "Expected hardware decoding to be active.");
        Assert.True(accessUnitCount > 0, "Expected at least one encoded access unit.");
        Assert.True(decodedFrameCount > 0, "Expected at least one decoded frame.");

        double fps = accessUnitCount / elapsedTime.TotalSeconds;
        string mode = queueEveryNthFrame == 1
            ? "all frames"
            : $"every {queueEveryNthFrame}th frame";
        _Output.WriteLine($"{sample.Codec} ({mode}): decoded {decodedFrameCount} frames from {accessUnitCount} access units in {elapsedTime.TotalMilliseconds:0} ms ({fps:F1} fps).");
    }

    private static int DrainDecodedFrames(IWindowsVideoDecoder decoder)
    {
        int count = 0;

        while (decoder.TryDequeueFrame(out WindowsDecodedFrame? frame))
        {
            using (frame)
            {
                count++;
            }
        }

        return count;
    }

    private record SampleVideo(string FilePath, CodecType Codec, int Width, int Height);
}
