// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.TrackingCore.BallTracking;

public class BallTests
{
    private const long _1s = 1000 * 1_000_000L;
    private const long _2s = 2 * _1s;

    private const double _LowPassAlphaDeltaXY = 0.5;
    private readonly Ball _Testee;
    private readonly Frame _Frame1 = new(41, 0);

    public BallTests()
    {
        _Testee = new(_LowPassAlphaDeltaXY);

        _Testee.Initialize(_Frame1, 3, 1.0, 2.0, TrackingEvidence.HighQualityObservation);
    }

    [Fact]
    public void Fixture()
    {
        Check(
            id: 3,
            x: 1.0,
            y: 2.0,
            evidence: TrackingEvidence.HighQualityObservation,
            distancePredicted: 0,
            velocity: new(),
            updateFrame: _Frame1,
            processFrame: _Frame1,
            wasUpdated: false);
    }

    [Fact]
    public void Get_Prediction()
    {
        Frame frame2 = new(42, _1s);
        _Testee.Update(frame2, 11.0, 22.0, 0.4, TrackingEvidence.HighQualityObservation);

        Frame frame3 = new(43, _2s);
        Point prediction = _Testee.GetPrediction(frame3);

        Assert.Equal(new(21.0, 42.0), prediction);
    }

    [Fact]
    public void First_update()
    {
        Frame frame2 = new(42, _1s);
        _Testee.Update(frame2, 11.0, 22.0, 0.4, TrackingEvidence.HighQualityObservation);

        Check(
            id: 3,
            x: 11.0,
            y: 22.0,
            evidence: TrackingEvidence.HighQualityObservation,
            distancePredicted: 0,
            velocity: new(10, 20),
            updateFrame: frame2,
            processFrame: frame2,
            wasUpdated: true);
    }

    [Fact]
    public void Second_update()
    {
        Frame frame2 = new(42, _1s);
        _Testee.Update(frame2, 11.0, 22.0, 0.4, TrackingEvidence.HighQualityObservation);

        Frame frame3 = new(43, _2s);
        _Testee.Update(frame3, 51.0, 122.0, 1.0, TrackingEvidence.VeryHighQualityObservation);

        Check(
            id: 3,
            x: 11.0 + 5 + 20,
            y: 22.0 + 10 + 50.0,
            evidence: TrackingEvidence.VeryHighQualityObservation,
            distancePredicted: 0,
            velocity: new(5 + 20, 10 + 50),
            updateFrame: frame3,
            processFrame: frame3,
            wasUpdated: true);
    }

    [Fact]
    public void Predict()
    {
        int s = 5;  // 5 seconds later
        double dx = 10 * s;
        double dy = 20 * s;

        Frame frame2 = new(42, _1s);
        _Testee.Update(frame2, 11.0, 22.0, 0.4, TrackingEvidence.HighQualityObservation);

        Frame frame3 = new(43, _1s + (_1s * 5));
        _Testee.Predict(frame3);

        Check(
            id: 3,
            x: 11 + (10 * s),
            y: 22 + (20 * s),
            evidence: TrackingEvidence.Prediction,
            distancePredicted: Math.Sqrt((dx * dx) + (dy * dy)),
            velocity: new(10, 20),
            updateFrame: frame2,
            processFrame: frame3,
            wasUpdated: true);
    }

    [Fact]
    public void Initialize_resets_observation_update_state()
    {
        Frame frame2 = new(42, _1s);
        _Testee.Update(frame2, 11.0, 22.0, 0.4, TrackingEvidence.HighQualityObservation);
        _Testee.Predict(new(43, _2s));

        Frame frame3 = new(44, _2s);
        _Testee.Initialize(frame3, 4, 3.0, 4.0, TrackingEvidence.VeryHighQualityObservation);

        Check(
            id: 4,
            x: 3.0,
            y: 4.0,
            evidence: TrackingEvidence.VeryHighQualityObservation,
            distancePredicted: 0,
            velocity: new(),
            updateFrame: frame3,
            processFrame: frame3,
            wasUpdated: false);
    }

    [Fact]
    public void Prediction_distance_does_not_cancel_out_on_negative_motion()
    {
        int s = 5;
        double dx = -10 * s;
        double dy = -20 * s;

        Frame frame2 = new(42, _1s);
        _Testee.Update(frame2, -9.0, -18.0, 0.4, TrackingEvidence.HighQualityObservation);

        Frame frame3 = new(43, _1s + (_1s * 5));
        _Testee.Predict(frame3);

        Check(
            id: 3,
            x: -9 + (-10 * s),
            y: -18 + (-20 * s),
            evidence: TrackingEvidence.Prediction,
            distancePredicted: Math.Sqrt((dx * dx) + (dy * dy)),
            velocity: new(-10, -20),
            updateFrame: frame2,
            processFrame: frame3,
            wasUpdated: true);
    }

    [Fact]
    public void Same_timestamp_update_does_not_divide_by_zero()
    {
        Frame frame2 = new(42, 0);

        _Testee.Update(frame2, 11.0, 22.0, 0.4, TrackingEvidence.HighQualityObservation);

        Check(
            id: 3,
            x: 11.0,
            y: 22.0,
            evidence: TrackingEvidence.HighQualityObservation,
            distancePredicted: 0,
            velocity: new(),
            updateFrame: frame2,
            processFrame: frame2,
            wasUpdated: true);
    }

    [Fact]
    public void Older_timestamp_update_is_ignored()
    {
        Frame frame2 = new(42, _1s);
        _Testee.Update(frame2, 11.0, 22.0, 0.4, TrackingEvidence.HighQualityObservation);

        _Testee.Update(new(43, _1s - 1), 51.0, 122.0, 1.0, TrackingEvidence.VeryHighQualityObservation);

        Check(
            id: 3,
            x: 11.0,
            y: 22.0,
            evidence: TrackingEvidence.HighQualityObservation,
            distancePredicted: 0,
            velocity: new(10, 20),
            updateFrame: frame2,
            processFrame: frame2,
            wasUpdated: true);
    }

    private void Check(
        int id,
        double x,
        double y,
        TrackingEvidence evidence,
        double distancePredicted,
        Vector2 velocity,
        Frame updateFrame,
        Frame processFrame,
        bool wasUpdated)
    {
        Assert.Equal(id, _Testee.Id);
        Assert.Equal(x, _Testee.X);
        Assert.Equal(y, _Testee.Y);
        Assert.Equal(evidence, _Testee.Evidence);
        Assert.Equal(distancePredicted, _Testee.DistancePredicted);
        Assert.Equal(velocity, _Testee.VelocityPxPerS);
        Assert.Equal(updateFrame, _Testee.LastUpdate);
        Assert.Equal(processFrame, _Testee.LastProcess);
        Assert.Equal(wasUpdated, _Testee.WasUpdatedByObservationAtLeastOnce);
    }
}
