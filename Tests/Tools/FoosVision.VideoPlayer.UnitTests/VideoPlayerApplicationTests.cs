// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Windows.Decoding;
using FoosVision.VideoPlayer.Options;
using FoosVision.VideoPlayer.Runtime;

namespace FoosVision.VideoPlayer.UnitTests;

public class VideoPlayerApplicationTests
{
    [Fact]
    public async Task RunAsync_starts_runtime_and_disposes_it_when_cancelled()
    {
        FakeVideoPlayerRuntime runtime = new();
        VideoPlayerApplication application = new(new FakeVideoPlayerRuntimeFactory(runtime));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        int exitCode = await application.RunAsync(CreateOptions(), cancellation.Token);

        Assert.Equal(0, exitCode);
        Assert.True(runtime.StartCalled);
        Assert.True(runtime.DisposeCalled);
    }

    [Fact]
    public async Task RunAsync_disposes_runtime_when_start_fails()
    {
        FakeVideoPlayerRuntime runtime = new() { ThrowOnStart = true };
        VideoPlayerApplication application = new(new FakeVideoPlayerRuntimeFactory(runtime));

        await Assert.ThrowsAsync<InvalidOperationException>(() => application.RunAsync(CreateOptions(), CancellationToken.None));
        Assert.True(runtime.StartCalled);
        Assert.True(runtime.DisposeCalled);
    }

    private static VideoPlayerOptions CreateOptions()
    {
        return new VideoPlayerOptions("clip.mp4", CodecType.H264, 1920, 1080, HardwareMode: WindowsVideoDecoderHardwareMode.PreferHardware);
    }

    private class FakeVideoPlayerRuntimeFactory(FakeVideoPlayerRuntime runtime) : IVideoPlayerRuntimeFactory
    {
        public IVideoPlayerRuntime Create(VideoPlayerOptions options)
        {
            Assert.NotNull(options);
            return runtime;
        }
    }

    private class FakeVideoPlayerRuntime : IVideoPlayerRuntime
    {
        public bool ThrowOnStart { get; set; }

        public bool StartCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public void Start()
        {
            StartCalled = true;

            if (ThrowOnStart)
            {
                throw new InvalidOperationException("Start failed.");
            }
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }
    }
}
