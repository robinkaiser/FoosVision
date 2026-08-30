// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Reflection;
using FoosVision.Media.Core.Capture;
using FoosVision.Media.Core.DecodedFrames;
using FoosVision.Media.Core.EncodedVideo;
using FoosVision.Media.Core.EncodedVideoStreaming;

namespace FoosVision.Media.Core.UnitTests;

public class CameraControllerTests
{
    [Fact]
    public async Task Start_starts_video_stream_session_before_camera_feed()
    {
        FakeCameraFeed cameraFeed = new();
        FakeVideoStreamSessionManager streamSessionManager = new();
        cameraFeed.StreamSessionManager = streamSessionManager;
        using CameraController controller = new(cameraFeed, streamSessionManager);

        await controller.Start(CancellationToken.None);

        Assert.Equal(1, streamSessionManager.StartSessionCallCount);
        Assert.Equal(1, cameraFeed.StartCallCount);
        Assert.True(cameraFeed.StreamSessionStartedBeforeFeedStart);
    }

    [Fact]
    public async Task Stop_stops_video_stream_session()
    {
        FakeCameraFeed cameraFeed = new();
        FakeVideoStreamSessionManager streamSessionManager = new();
        using CameraController controller = new(cameraFeed, streamSessionManager);

        await controller.Stop(CancellationToken.None);

        Assert.Equal(1, cameraFeed.StopCallCount);
        Assert.Equal(1, streamSessionManager.StopSessionCallCount);
    }

    [Fact]
    public async Task Start_bootstraps_stream_from_first_keyframe()
    {
        FakeCameraFeed cameraFeed = new();
        FakeVideoStreamSessionManager streamSessionManager = new();
        using CameraController controller = new(cameraFeed, streamSessionManager);

        await controller.Start(CancellationToken.None);

        InvokeOnEncodedUnitReady(controller, new EncodedAccessUnit(10, false, true, 0, 4));
        Assert.Empty(streamSessionManager.EnqueuedUnits);

        InvokeOnEncodedUnitReady(controller, new EncodedAccessUnit(20, true, true, 0, 4));
        Assert.Single(streamSessionManager.EnqueuedUnits);

        InvokeOnEncodedUnitReady(controller, new EncodedAccessUnit(30, false, true, 4, 4));
        Assert.Equal(2, streamSessionManager.EnqueuedUnits.Count);
    }

    private static void InvokeOnEncodedUnitReady(CameraController controller, EncodedAccessUnit unit)
    {
        MethodInfo method = typeof(CameraController).GetMethod("OnEncodedUnitReady", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(controller, [unit]);
    }

    private class FakeCameraFeed : ICameraFeed
    {
        public int StartCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public bool StreamSessionStartedBeforeFeedStart { get; private set; }

        public FakeVideoStreamSessionManager? StreamSessionManager { get; set; }

        public Task<bool> Configure()
        {
            return Task.FromResult(true);
        }

        public Task<bool> Start(IFrameSink frameSink, IEncodedAccessUnitSink encodedUnitSink)
        {
            StartCallCount++;
            StreamSessionStartedBeforeFeedStart = StreamSessionManager?.StartSessionCallCount == 1;
            return Task.FromResult(true);
        }

        public Task Stop()
        {
            StopCallCount++;
            return Task.CompletedTask;
        }
    }

    private class FakeVideoStreamSessionManager : IVideoStreamSessionManager
    {
        public int StartSessionCallCount { get; private set; }

        public int StopSessionCallCount { get; private set; }

        public List<EncodedUnitEnvelope> EnqueuedUnits { get; } = [];

        public void Configure(string ipAddress, int port)
        {
        }

        public void StartSession()
        {
            StartSessionCallCount++;
        }

        public void StopSession()
        {
            StopSessionCallCount++;
        }

        public void Enqueue(byte[] buffer, int offset, int length, long timeNs, bool markAsEndOfAccessUnit)
        {
            EnqueuedUnits.Add(new EncodedUnitEnvelope(offset, length, timeNs, markAsEndOfAccessUnit));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    private readonly record struct EncodedUnitEnvelope(int Offset, int Length, long TimeNs, bool MarkAsEndOfAccessUnit);
}
