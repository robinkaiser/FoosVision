// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.Adapters.Viewer.Session.Replay;

public class ReplayVisionOps : IReplayVisionOps
{
    private readonly IBallFinder _BallFinder;
    private readonly ReplayFrame _Frame;

    public ReplayVisionOps(IBallFinder ballFinder, ReplayFrame frame)
    {
        _BallFinder = ballFinder;
        _Frame = frame;
    }

    public IReadOnlyList<ObservedBall> DetectBalls(TableConfiguration tableConfiguration, Rectangle regionOfInterest)
    {
        return _BallFinder.DetectYuv420(
            _Frame.Frame.BufferY,
            _Frame.Frame.BufferU,
            _Frame.Frame.BufferV,
            _Frame.Frame.Layout.Width,
            _Frame.Frame.Layout.Height,
            _Frame.Frame.Layout.Y.RowStride,
            _Frame.Frame.Layout.Y.PixelStride,
            _Frame.Frame.Layout.U.RowStride,
            _Frame.Frame.Layout.U.PixelStride,
            _Frame.Frame.Layout.V.RowStride,
            _Frame.Frame.Layout.V.PixelStride,
            tableConfiguration,
            regionOfInterest);
    }
}
