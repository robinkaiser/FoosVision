// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.UnitTests.Replay;

internal class RecordingReplayVisionOps : IReplayVisionOps
{
    private readonly Queue<IReadOnlyList<ObservedBall>> _Observations;

    public RecordingReplayVisionOps(params IReadOnlyList<ObservedBall>[] observations)
    {
        _Observations = new Queue<IReadOnlyList<ObservedBall>>(observations);
    }

    public int DetectBallsCallCount { get; private set; }

    public Rectangle? LastRegionOfInterest { get; private set; }

    public IReadOnlyList<ObservedBall> DetectBalls(TableConfiguration tableConfiguration, Rectangle regionOfInterest)
    {
        DetectBallsCallCount++;
        LastRegionOfInterest = regionOfInterest;
        return _Observations.Count == 0 ? [] : _Observations.Dequeue();
    }
}
