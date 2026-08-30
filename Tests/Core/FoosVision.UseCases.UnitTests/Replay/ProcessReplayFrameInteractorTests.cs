// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.UseCases.Replay.ProcessReplayFrame;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Replay;

public class ProcessReplayFrameInteractorTests
{
    private readonly FakeReplaySessionStore _FakeStore = new();
    private readonly IProcessReplayFrameOutputPort _Output;
    private readonly List<ReplayFrameProcessedResponse> _Processed = [];
    private readonly ProcessReplayFrameInteractor _Testee;

    public ProcessReplayFrameInteractorTests()
    {
        _Output = Substitute.For<IProcessReplayFrameOutputPort>();
        _Output.ReportReplayFrameProcessed(Arg.Any<ReplayFrameProcessedResponse>())
            .Returns(ci =>
            {
                _Processed.Add(ci.Arg<ReplayFrameProcessedResponse>());
                return Task.CompletedTask;
            });
        _Testee = new ProcessReplayFrameInteractor(_FakeStore);
    }

    [Fact]
    public async Task Process_frame_uses_vision_and_tracks_observations()
    {
        ReplayId replayId = SaveReplay();
        RecordingReplayVisionOps vision = new([new ObservedBall(new Point(300, 200), 0.8)]);

        await _Testee.Handle(
            new ProcessReplayFrameRequest(new Frame(1, 2_000_000_000), vision),
            _Output,
            CancellationToken.None);

        Assert.Equal(1, vision.DetectBallsCallCount);
        Assert.Equal(new Rectangle(-92, 8, 384, 384), vision.LastRegionOfInterest);
        ReplayFrameProcessedResponse processed = Assert.Single(_Processed);
        Assert.Equal(replayId, processed.ReplayId);
    }

    [Fact]
    public async Task Process_frame_skips_frames_at_or_before_track_anchor_without_running_vision()
    {
        SaveReplay();
        RecordingReplayVisionOps vision = new([new ObservedBall(new Point(300, 200), 0.8)]);

        await _Testee.Handle(
            new ProcessReplayFrameRequest(new Frame(1, 1_000_000_000), vision),
            _Output,
            CancellationToken.None);

        Assert.Equal(0, vision.DetectBallsCallCount);
        Assert.Empty(_Processed);
        await _Output.Received().ReportSkipped("Replay frame is not after the track anchor.");
    }

    private ReplayId SaveReplay()
    {
        ReplayId replayId = new(42, 1_000_000);
        _FakeStore.SaveActive(ReplaySessionTestFactory.CreateStartedWithObservationTracking(replayId));
        return replayId;
    }
}
