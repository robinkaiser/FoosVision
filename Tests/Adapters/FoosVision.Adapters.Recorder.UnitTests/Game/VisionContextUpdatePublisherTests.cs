// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Game;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Recorder.UnitTests.Game;

public class VisionContextUpdatePublisherTests
{
    [Fact]
    public async Task Publishes_color_response_image_when_enabled()
    {
        byte[] buffer = [1, 2, 3, 4];
        FakeVisionContextProvider visionContextProvider = new(buffer);
        FakeLiveAnalysisPublisher publisher = new();

        using VisionContextUpdatePublisher testee = new(
            visionContextProvider,
            publisher,
            isEnabled: () => true,
            interval: TimeSpan.FromMilliseconds(10));

        VisionContextMessage message = await publisher.WaitForVisionContext();

        Assert.Same(buffer, message.Buffer);
        Assert.Equal(buffer.Length, message.Length);
        Assert.True(visionContextProvider.CallCount >= 1);
    }

    [Fact]
    public async Task Skips_publish_when_disabled()
    {
        FakeVisionContextProvider visionContextProvider = new([1, 2, 3, 4]);
        FakeLiveAnalysisPublisher publisher = new();

        using VisionContextUpdatePublisher testee = new(
            visionContextProvider,
            publisher,
            isEnabled: () => false,
            interval: TimeSpan.FromMilliseconds(10));

        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(0, visionContextProvider.CallCount);
        Assert.Equal(0, publisher.PublishVisionContextCallCount);
    }

    private class FakeVisionContextProvider : IEncodedVisionContextProvider
    {
        private readonly byte[] _Buffer;

        public FakeVisionContextProvider(byte[] buffer)
        {
            _Buffer = buffer;
        }

        public int CallCount { get; private set; }

        public bool TryGetEncodedVisionContext(out EncodedVisionContext context)
        {
            CallCount++;
            context = new EncodedVisionContext(_Buffer, _Buffer.Length);
            return true;
        }
    }

    private class FakeLiveAnalysisPublisher : IRecorderLiveAnalysisPublisher
    {
        private readonly TaskCompletionSource<VisionContextMessage> _VisionContextPublished = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int PublishVisionContextCallCount { get; private set; }

        public Task PublishReplayStarted(ReplayStartedMessage replayStarted, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishReplay(ReplayMessage replay, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task PublishVisionContext(VisionContextMessage visionContext, CancellationToken ct = default)
        {
            PublishVisionContextCallCount++;
            _VisionContextPublished.TrySetResult(visionContext);
            return Task.CompletedTask;
        }

        public Task PublishBallDetectionMask(BallDetectionMaskMessage ballDetectionMask, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task<VisionContextMessage> WaitForVisionContext()
        {
            return _VisionContextPublished.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }
    }
}
