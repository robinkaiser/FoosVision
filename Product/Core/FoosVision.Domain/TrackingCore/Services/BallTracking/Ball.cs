// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.BallTracking;

public class Ball
{
    private readonly double _LowPassAlphaDeltaXY;
    private readonly LowPassFilter _DeltaX;
    private readonly LowPassFilter _DeltaY;

    public Ball(double lowPassAlphaDeltaXY)
    {
        _LowPassAlphaDeltaXY = lowPassAlphaDeltaXY;
        _DeltaX = new LowPassFilter(_LowPassAlphaDeltaXY);
        _DeltaY = new LowPassFilter(_LowPassAlphaDeltaXY);
    }

    public int Id { get; private set; }

    public double X { get; private set; }

    public double Y { get; private set; }

    public TrackingEvidence Evidence { get; private set; }

    public double DistancePredicted { get; private set; }

    public Vector2 VelocityPxPerS => new(_DeltaX.Last, _DeltaY.Last);

    // Last update with observation
    public Frame LastUpdate { get; private set; }

    // Last update or predict
    public Frame LastProcess { get; private set; }

    public bool WasUpdatedByObservationAtLeastOnce { get; private set; }

    public void Initialize(Frame frame, int id, double x, double y, TrackingEvidence evidence)
    {
        Id = id;
        X = x;
        Y = y;
        Evidence = evidence;
        DistancePredicted = 0.0;
        WasUpdatedByObservationAtLeastOnce = false;
        UpdateType = TrackedBallUpdateType.Observed;

        _DeltaX.Reset(0.0);
        _DeltaY.Reset(0.0);

        LastUpdate = frame;
        LastProcess = frame;
    }

    public Point GetPrediction(Frame frame)
    {
        var m = GetPredictedMovement(frame.TimestampNs);

        double x = X + m.X;
        double y = Y + m.Y;

        return new(x, y);
    }

    public void Update(Frame frame, double observedX, double observedY, double observedQuality, TrackingEvidence evidence)
    {
        double secondsElapsed = (frame.TimestampNs - LastProcess.TimestampNs) / 1_000_000_000.0;

        if (secondsElapsed < 0.0)
        {
            return;
        }

        if (secondsElapsed == 0.0)
        {
            X = observedX;
            Y = observedY;
            Evidence = evidence;
            DistancePredicted = 0.0;
            WasUpdatedByObservationAtLeastOnce = true;
            UpdateType = TrackedBallUpdateType.Observed;

            LastUpdate = frame;
            LastProcess = frame;
            return;
        }

        double alphaDeltaXY = _LowPassAlphaDeltaXY * observedQuality;
        alphaDeltaXY = Math.Min(1.0, alphaDeltaXY);

        if (!WasUpdatedByObservationAtLeastOnce)
        {   // First update will always utilize the full motion vector regardless of observation quality
            alphaDeltaXY = 1.0;
            WasUpdatedByObservationAtLeastOnce = true;
        }

        double dXperSec = (observedX - X) / secondsElapsed;
        double dYPerSec = (observedY - Y) / secondsElapsed;

        _DeltaX.Filter(dXperSec, alphaDeltaXY);
        _DeltaY.Filter(dYPerSec, alphaDeltaXY);

        X += _DeltaX.Last * secondsElapsed;
        Y += _DeltaY.Last * secondsElapsed;
        Evidence = evidence;
        DistancePredicted = 0.0;
        UpdateType = TrackedBallUpdateType.Observed;

        LastUpdate = frame;
        LastProcess = frame;
    }

    public void Predict(Frame frame)
    {
        var m = GetPredictedMovement(frame.TimestampNs);

        X += m.X;
        Y += m.Y;
        Evidence = TrackingEvidence.Prediction;
        DistancePredicted += Math.Sqrt((m.X * m.X) + (m.Y * m.Y));
        UpdateType = TrackedBallUpdateType.Predicted;

        LastProcess = frame;
    }

    internal TrackedBallUpdateType UpdateType { get; private set; }

    private Vector2 GetPredictedMovement(long timeStamp_ns)
    {
        double secondsElapsed = (timeStamp_ns - LastProcess.TimestampNs) / 1_000_000_000.0;
        double dx = _DeltaX.Last * secondsElapsed;
        double dy = _DeltaY.Last * secondsElapsed;

        return new(dx, dy);
    }
}
