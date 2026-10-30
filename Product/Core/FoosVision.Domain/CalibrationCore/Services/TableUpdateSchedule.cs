// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.CalibrationCore.Services;

public class TableUpdateSchedule
{
    private const long _TableConfigUpdateInterval_ns = 10000L * 1_000_000L;
    private const long _TableSceneUpdateInterval_ns = 500L * 1_000_000L;
    private const int _TableSceneUpdateMinDistance_px = 50;

    private Frame _LastTableConfigUpdateFrame;
    private bool _TableUpdateInProgress;

    private Frame _LastTableSceneUpdateFrame;
    private Point _LastTableSceneUpdatePosition;
    private bool _TableSceneUpdateInProgress;

    public void CompleteTableUpdate() => _TableUpdateInProgress = false;

    public void CompleteTableSceneUpdate() => _TableSceneUpdateInProgress = false;

    public bool RequestTableUpdate(Frame frame)
    {
        if (HasCalibrationUpdateInProgress) return false;

        var isDue = (frame.TimestampNs - _LastTableConfigUpdateFrame.TimestampNs) >= _TableConfigUpdateInterval_ns;
        if (!isDue) return false;

        _LastTableConfigUpdateFrame = frame;
        _TableUpdateInProgress = true;

        return true;
    }

    public bool RequestTableSceneUpdate(Frame frame, bool isBallFound, Point ballPosition, IReadOnlyList<TrackedBall> candidates)
    {
        if (HasCalibrationUpdateInProgress) return false;
        if (!TryGetFoundBall(isBallFound, ballPosition, candidates, out TrackedBall? trackedBall)) return false;

        var isDue = (frame.TimestampNs - _LastTableSceneUpdateFrame.TimestampNs) >= _TableSceneUpdateInterval_ns;
        if (!isDue) return false;

        var hasHighConfidence = trackedBall.Confidence == TrackingConfidence.High;
        if (!hasHighConfidence) return false;

        var thisX = trackedBall.Position.X;
        var thisY = trackedBall.Position.Y;
        var lastX = _LastTableSceneUpdatePosition.X;
        var lastY = _LastTableSceneUpdatePosition.Y;
        var dX = thisX - lastX;
        var dY = thisY - lastY;

        double distance = Math.Sqrt((dX * dX) + (dY * dY));
        var hasMoved = distance >= _TableSceneUpdateMinDistance_px;
        if (!hasMoved) return false;

        _LastTableSceneUpdateFrame = frame;
        _LastTableSceneUpdatePosition = trackedBall.Position;
        _TableSceneUpdateInProgress = true;

        return true;
    }

    private static bool TryGetFoundBall(bool isBallFound, Point ballPosition, IReadOnlyList<TrackedBall> candidates, [NotNullWhen(true)] out TrackedBall? trackedBall)
    {
        trackedBall = candidates.FirstOrDefault(candidate =>
            candidate.Status == TrackingStatus.Observed &&
            candidate.Position == ballPosition);

        return
            isBallFound &&
            trackedBall != null;
    }

    private bool HasCalibrationUpdateInProgress =>
        _TableUpdateInProgress ||
        _TableSceneUpdateInProgress;
}
