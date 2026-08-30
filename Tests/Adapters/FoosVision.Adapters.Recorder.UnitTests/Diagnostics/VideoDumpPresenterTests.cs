// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Adapters.Common.Live;
using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Adapters.Recorder.Game.Control;
using FoosVision.Adapters.Recorder.Installation.Control;
using FoosVision.Ports.Media;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.UseCases.Game.StopGame;
using FoosVision.UseCases.Installation.StopInstall;

namespace FoosVision.Adapters.Recorder.UnitTests.Diagnostics;

public class VideoDumpPresenterTests
{
    [Fact]
    public async Task InstallEventPresenter_schedules_installation_dump_after_successful_stop()
    {
        RecordingVideoDumpOrchestrator videoDumpOrchestrator = new();
        InstallEventPresenter testee = new(
            CreateFrameLoop(),
            new RecorderRuntimeStateController(new RecordingEventPublisher()),
            videoDumpOrchestrator);

        await testee.ReportStopped(new StopInstallResponse(Guid.NewGuid()));

        Assert.Equal(new[] { VideoDumpSessionKind.Installation }, videoDumpOrchestrator.SessionKinds);
    }

    [Fact]
    public async Task GameEventPresenter_schedules_game_dump_after_successful_stop()
    {
        RecordingVideoDumpOrchestrator videoDumpOrchestrator = new();
        GameEventPresenter testee = new(
            CreateFrameLoop(),
            new RecorderRuntimeStateController(new RecordingEventPublisher()),
            videoDumpOrchestrator);

        await testee.ReportStopped(new StopGameResponse(Guid.NewGuid()));

        Assert.Equal(new[] { VideoDumpSessionKind.Game }, videoDumpOrchestrator.SessionKinds);
    }

    private static FrameProcessingLoop CreateFrameLoop()
    {
        return new FrameProcessingLoop(new FakeFrameFeed(), new FakeFrameProcessor());
    }

    private class RecordingVideoDumpOrchestrator : IVideoDumpOrchestrator
    {
        public List<VideoDumpSessionKind> SessionKinds { get; } = [];

        public bool TryScheduleDump(VideoDumpSessionKind sessionKind)
        {
            SessionKinds.Add(sessionKind);
            return true;
        }
    }

    private class RecordingEventPublisher : IRecorderEventPublisher
    {
        public List<object> Events { get; } = [];

        public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct)
        {
            Events.Add(evt!);
            return Task.CompletedTask;
        }
    }

    private class FakeFrameFeed : IFrameFeed
    {
        public event Action<IFrameHandle>? FrameReady;

        public bool TryAcquireById(ulong id, [NotNullWhen(true)] out IFrameHandle? handle)
        {
            handle = null;
            return false;
        }

        public void RaiseFrameReady(IFrameHandle handle)
        {
            FrameReady?.Invoke(handle);
        }
    }

    private class FakeFrameProcessor : IFrameProcessor
    {
        public bool ShouldProcess => false;

        public Task Process([NotNull] IFrameHandle frame, CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
