// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.Services;

namespace FoosVision.Domain.UnitTests.Training;

public class SequenceRecognizerTests
{
    private readonly SequenceRecognizer _Testee;
    private readonly PossessionCalculator _PossessionCalculator;

    private int _FrameId;
    private ulong _BallId;

    public SequenceRecognizerTests()
    {
        _Testee = new SequenceRecognizer(TableConfig.Config);
        _PossessionCalculator = new(TableConfig.Config);
    }

    [Fact]
    public void Fixture()
    {
    }

    [Fact]
    public void Full_sequence_for_team_A_pass()
    {
        Assert.Equal(SequenceState.Idle, Process(1000, new(700, 700)));
        Assert.Equal(SequenceState.PassSetupCompleted, Process(2000, new(710, 710)));
        Assert.Equal(SequenceState.PassSequenceRunning, Process(3000, new(741, 740)));
        Assert.Equal(SequenceState.SequenceCompleted, Process(4000, new(1100, 700)));
    }

    [Fact]
    public void Full_sequence_for_team_B_pass()
    {
        Assert.Equal(SequenceState.Idle, Process(1000, new(900, 200)));
        Assert.Equal(SequenceState.PassSetupCompleted, Process(2000, new(910, 210)));
        Assert.Equal(SequenceState.PassSequenceRunning, Process(3000, new(941, 240)));
        Assert.Equal(SequenceState.SequenceCompleted, Process(4000, new(500, 200)));
    }

    [Fact]
    public void Full_sequence_for_team_A_shot()
    {
        Assert.Equal(SequenceState.Idle, Process(1000, new(1100, 200)));
        Assert.Equal(SequenceState.ShotSetupCompleted, Process(2000, new(1110, 210)));
        Assert.Equal(SequenceState.ShotSequenceRunning, Process(3000, new(1141, 240)));
        Assert.Equal(SequenceState.SequenceCompleted, Process(4000, new(1300, 500)));
    }

    [Fact]
    public void Full_sequence_for_team_B_shot()
    {
        Assert.Equal(SequenceState.Idle, Process(1000, new(500, 600)));
        Assert.Equal(SequenceState.ShotSetupCompleted, Process(2000, new(510, 610)));
        Assert.Equal(SequenceState.ShotSequenceRunning, Process(3000, new(541, 640)));
        Assert.Equal(SequenceState.SequenceCompleted, Process(4000, new(300, 200)));
    }

    private SequenceState Process(long time_ms, Point position)
    {
        Frame frame = new(_BallId++, time_ms * 1_000_000);
        TrackedBall trackedBall = new(_FrameId++, frame, position, TrackingConfidence.High, TrackingStatus.Observed, new());
        BallPossession possession = _PossessionCalculator.Compute(position);

        return _Testee.Process(trackedBall, possession);
    }
}
