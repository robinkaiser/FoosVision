// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics;
using FoosVision.Common.Metrics;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.Adapters.Recorder.Game.Live;

public class FrameVisionOps : IFrameVisionOps
{
    private readonly IBallFinder _BallFinder;
    private readonly IFrameHandle _Frame;
    private readonly DurationMetric? _DetectBallsDuration;

    public FrameVisionOps(
        IBallFinder ballFinder,
        IFrameHandle frame,
        DurationMetric? detectBallsDuration = null)
    {
        _BallFinder = ballFinder;
        _Frame = frame;
        _DetectBallsDuration = detectBallsDuration;
    }

    public IReadOnlyList<ObservedBall> DetectBalls(TableConfiguration config)
    {
        DurationMetric? duration = _DetectBallsDuration;

        if (duration == null)
        {
            return _BallFinder.Detect(_Frame.BufferRGBA8888, config);
        }

        long started = Stopwatch.GetTimestamp();
        try
        {
            return _BallFinder.Detect(_Frame.BufferRGBA8888, config);
        }
        finally
        {
            duration.RecordElapsed(started);
        }
    }
}
