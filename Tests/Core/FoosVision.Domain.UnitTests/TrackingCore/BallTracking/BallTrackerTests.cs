// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.TrackingCore.BallTracking;

public class BallTrackerTests
{
    private const long _1s = 1000 * 1_000_000L;
    private const long _2s = 2 * _1s;
    private const long _3s = 3 * _1s;

    private readonly BallTracker _Testee;
    private readonly BallTrackerParams _Params;
    private readonly ObservationQualityThresholds _QualityThresholds;

    public BallTrackerTests()
    {
        _Params = BallTrackerParams.Default with
        {
            MaxTrackedBallsCount = 5,
            TrackedBallMaxPredictionDistancePx = 100,
            TrackedBallMaxUnobservedTime = TimeSpan.FromSeconds(2),
        };
        _QualityThresholds = ObservationQualityThresholds.Default;

        _Testee = new BallTracker(_Params, TableConfig.Config);
    }

    [Fact]
    public void Fixture()
    {
        Assert.Null(_Testee.Latest);
    }

    [Fact]
    public void Default_max_unobserved_time_is_short_visual_loss_window()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(500), BallTrackerParams.Default.TrackedBallMaxUnobservedTime);
    }

    [Fact]
    public void Default_reacquisition_time_keeps_recent_lost_tracks_available()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), BallTrackerParams.Default.TrackedBallReacquisitionTime);
    }

    [Theory]
    [InlineData(0.14, ObservationQualityLevel.BelowMinimum)]
    [InlineData(0.15, ObservationQualityLevel.LowQuality)]
    [InlineData(0.50, ObservationQualityLevel.HighQuality)]
    [InlineData(0.70, ObservationQualityLevel.VeryHighQuality)]
    public void Observed_ball_classifies_quality(double quality, ObservationQualityLevel expected)
    {
        ObservedBall observedBall = new(new Point(200, 300), quality);

        Assert.Equal(expected, observedBall.QualityLevel);
    }

    [Fact]
    public void First_update_with_no_observations()
    {
        var snapshot = _Testee.ApplyObservations(new(1, _1s), []);

        Assert.Null(snapshot.Current);
    }

    [Fact]
    public void Low_quality_observation_is_discarded()
    {
        var snapshot = _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality - 0.01));

        Assert.Null(snapshot.Current);
    }

    [Fact]
    public void Minimum_quality_observation_is_tracked()
    {
        var snapshot = _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));

        Verify(snapshot.Current, 0, 1, _1s, TrackingConfidence.Average, TrackingStatus.Observed);
        Assert.Empty(snapshot.OtherCandidates);
    }

    [Fact]
    public void Too_many_tracked_balls_overflow()
    {
        var snapshot = _Testee.ApplyObservations(new(1, _1s), GetObservations(
            200, 200, _QualityThresholds.HighQuality,
            300, 300, _QualityThresholds.HighQuality,
            400, 400, _QualityThresholds.HighQuality,
            500, 500, _QualityThresholds.HighQuality,
            600, 600, _QualityThresholds.HighQuality,
            700, 700, _QualityThresholds.HighQuality));

        Assert.NotNull(snapshot.Current);
        Assert.Equal(_Params.MaxTrackedBallsCount - 1, snapshot.OtherCandidates.Count());
    }

    [Fact]
    public void Update_tracked_ball_with_nearby_low_quality_observation()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        _Testee.ApplyObservations(new(2, _2s), GetObservations(200 + _Params.TrackedBallMaxNearByDistanceForObservationMatchingPx, 300, _QualityThresholds.MinQuality));

        Verify(_Testee.Latest!.Current, 0, 2, _2s, TrackingConfidence.Low, TrackingStatus.Observed);
    }

    [Fact]
    public void Update_tracked_ball_with_nearby_high_quality_observation()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        _Testee.ApplyObservations(new(2, _2s), GetObservations(200 + _Params.TrackedBallMaxNearByDistanceForObservationMatchingPx, 300, _QualityThresholds.VeryHighQuality));

        Verify(_Testee.Latest!.Current, 0, 2, _2s, TrackingConfidence.High, TrackingStatus.Observed);
    }

    [Fact]
    public void Matching_prioritizes_higher_evidence_observations()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));

        var snapshot = _Testee.ApplyObservations(new(2, _2s), GetObservations(
            201, 300, _QualityThresholds.MinQuality,
            202, 300, _QualityThresholds.VeryHighQuality));

        Verify(snapshot.Current, 0, 2, _2s, TrackingConfidence.High, TrackingStatus.Observed);
        Assert.Equal(new(202, 300), snapshot.Current!.Position);
        Assert.Empty(snapshot.OtherCandidates);
    }

    [Fact]
    public void Update_tracked_ball_with_quite_nearby_Observation()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        _Testee.ApplyObservations(new(2, _2s), GetObservations(200 + _Params.TrackedBallMaxQuiteNearByDistanceForObservationMatchingPx, 300, _QualityThresholds.HighQuality));

        Verify(_Testee.Latest!.Current, 0, 2, _2s, TrackingConfidence.Average, TrackingStatus.Observed);
    }

    [Fact]
    public void Do_not_update_tracked_ball_with_too_far_away_observation_and_retire_low_quality_ball()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.VeryHighQuality));
        _Testee.ApplyObservations(new(2, _2s), GetObservations(201, 300, _QualityThresholds.VeryHighQuality));
        _Testee.ApplyObservations(new(3, _3s), GetObservations(200 + _Params.TrackedBallMaxQuiteNearByDistanceForObservationMatchingPx, 300, _QualityThresholds.HighQuality));

        Verify(_Testee.Latest!.Current, 1, 3, _3s, TrackingConfidence.Average, TrackingStatus.Observed);
        Assert.Empty(_Testee.Latest!.OtherCandidates);
    }

    [Fact]
    public void Predict_tracked_ball()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        _Testee.ApplyObservations(new(2, _1s + (int)_Params.TrackedBallMaxUnobservedTime.TotalNanoseconds), []);

        Verify(_Testee.Latest!.Current, 0, 2, _1s + (int)_Params.TrackedBallMaxUnobservedTime.TotalNanoseconds, TrackingConfidence.Low, TrackingStatus.Predicted);
    }

    [Fact]
    public void Other_candidates_carry_tracking_status()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(
            200, 300, _QualityThresholds.HighQuality,
            400, 300, _QualityThresholds.HighQuality));

        var observedSnapshot = _Testee.Latest!;

        Assert.All(observedSnapshot.OtherCandidates, candidate => Assert.Equal(TrackingStatus.Observed, candidate.Status));

        _Testee.ApplyObservations(new(2, _2s), []);

        var predictedSnapshot = _Testee.Latest!;

        Assert.NotNull(predictedSnapshot.Current);
        Assert.Equal(TrackingStatus.Predicted, predictedSnapshot.Current.Status);
        Assert.All(predictedSnapshot.OtherCandidates, candidate => Assert.Equal(TrackingStatus.Predicted, candidate.Status));
    }

    [Fact]
    public void Snapshot_candidates_include_current_and_other_candidates()
    {
        var snapshot = _Testee.ApplyObservations(new(1, _1s), GetObservations(
            200, 300, _QualityThresholds.HighQuality,
            400, 300, _QualityThresholds.HighQuality,
            600, 300, _QualityThresholds.HighQuality));

        Assert.NotNull(snapshot.Current);
        Assert.Equal(3, snapshot.Candidates.Count);
        Assert.Equal(snapshot.Current, snapshot.Candidates[0]);
        Assert.Equal(snapshot.OtherCandidates, snapshot.Candidates.Skip(1));
    }

    [Fact]
    public void Candidates_carry_evidence_and_unobserved_age()
    {
        var lowEvidenceTracker = new BallTracker(_Params, TableConfig.Config);
        lowEvidenceTracker.ApplyObservations(new(1, _1s), GetObservations(400, 300, _QualityThresholds.HighQuality));
        lowEvidenceTracker.ApplyObservations(new(2, _2s), GetObservations(400, 300, _QualityThresholds.MinQuality));

        var lowEvidenceCandidate = Assert.Single(lowEvidenceTracker.Latest!.Candidates);

        Assert.Equal(TrackingEvidence.LowQualityObservation, lowEvidenceCandidate.Evidence);
        Assert.Equal(0, lowEvidenceCandidate.UnobservedAgeMs);

        var highEvidenceTracker = new BallTracker(_Params, TableConfig.Config);
        highEvidenceTracker.ApplyObservations(new(1, _1s), GetObservations(
            600, 300, _QualityThresholds.HighQuality,
            800, 300, _QualityThresholds.HighQuality));
        highEvidenceTracker.ApplyObservations(new(2, _2s), GetObservations(
            600, 300, _QualityThresholds.HighQuality,
            800, 300, _QualityThresholds.VeryHighQuality));

        var observedSnapshot = highEvidenceTracker.Latest!;

        Assert.Contains(observedSnapshot.Candidates, candidate =>
            candidate.Evidence == TrackingEvidence.HighQualityObservation
            && candidate.UnobservedAgeMs == 0);
        Assert.Contains(observedSnapshot.Candidates, candidate =>
            candidate.Evidence == TrackingEvidence.VeryHighQualityObservation
            && candidate.UnobservedAgeMs == 0);

        highEvidenceTracker.ApplyObservations(new(3, _2s + 250_000_000), []);

        var predictedSnapshot = highEvidenceTracker.Latest!;

        Assert.All(predictedSnapshot.Candidates, candidate =>
        {
            Assert.Equal(TrackingEvidence.Prediction, candidate.Evidence);
            Assert.Equal(250, candidate.UnobservedAgeMs);
        });
    }

    [Fact]
    public void Retire_tracked_balls_with_long_unobserved_time()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        _Testee.ApplyObservations(new(2, _1s + (int)_Params.TrackedBallMaxUnobservedTime.TotalNanoseconds + 1), []);

        Assert.Null(_Testee.Latest!.Current);
    }

    [Fact]
    public void Retire_tracked_balls_with_long_unobserved_time_before_even_updating()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        _Testee.ApplyObservations(
            new(2, _1s + (int)_Params.TrackedBallMaxUnobservedTime.TotalNanoseconds + 1),
            GetObservations(200 + _Params.TrackedBallMaxNearByDistanceForObservationMatchingPx, 300, _QualityThresholds.MinQuality));

        Assert.Null(_Testee.Latest!.Current);
    }

    [Fact]
    public void Low_quality_observation_near_recently_retired_track_reacquires_ball()
    {
        var tracker = new BallTracker(BallTrackerParams.Default, TableConfig.Config);
        tracker.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        tracker.ApplyObservations(
            new(2, _1s + (int)BallTrackerParams.Default.TrackedBallMaxUnobservedTime.TotalNanoseconds + 1),
            []);

        var snapshot = tracker.ApplyObservations(
            new(3, _1s + 600_000_000),
            GetObservations(210, 300, _QualityThresholds.MinQuality));

        Verify(snapshot.Current, 1, 3, _1s + 600_000_000, TrackingConfidence.Low, TrackingStatus.Observed);
    }

    [Fact]
    public void Low_quality_observation_far_from_recently_retired_track_is_discarded()
    {
        var tracker = new BallTracker(BallTrackerParams.Default, TableConfig.Config);
        tracker.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        tracker.ApplyObservations(
            new(2, _1s + (int)BallTrackerParams.Default.TrackedBallMaxUnobservedTime.TotalNanoseconds + 1),
            []);

        var snapshot = tracker.ApplyObservations(
            new(3, _1s + 600_000_000),
            GetObservations(200 + BallTrackerParams.Default.TrackedBallMaxReacquisitionDistancePx + 1, 300, _QualityThresholds.MinQuality));

        Assert.Null(snapshot.Current);
    }

    [Fact]
    public void Low_quality_observation_after_reacquisition_time_is_discarded()
    {
        var tracker = new BallTracker(BallTrackerParams.Default, TableConfig.Config);
        tracker.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        tracker.ApplyObservations(
            new(2, _1s + (int)BallTrackerParams.Default.TrackedBallMaxUnobservedTime.TotalNanoseconds + 1),
            []);

        var snapshot = tracker.ApplyObservations(
            new(3, _1s + (long)BallTrackerParams.Default.TrackedBallReacquisitionTime.TotalNanoseconds + 1),
            GetObservations(200, 300, _QualityThresholds.MinQuality));

        Assert.Null(snapshot.Current);
    }

    [Fact]
    public void Retire_tracked_balls_with_long_prediction_distance()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(200, 300, _QualityThresholds.HighQuality));
        _Testee.ApplyObservations(new(2, _2s), GetObservations(300, 400 + 1, _QualityThresholds.HighQuality)); // Speed: ~142 px/s
        _Testee.ApplyObservations(new(3, _3s), []);

        Assert.Null(_Testee.Latest!.Current);
    }

    [Fact]
    public void Retire_tracked_balls_outside_of_playing_field()
    {
        _Testee.ApplyObservations(new(1, _1s), GetObservations(
          200, 200, _QualityThresholds.HighQuality,
          200, 700, _QualityThresholds.HighQuality,
          700, 200, _QualityThresholds.HighQuality,
          700, 700, _QualityThresholds.HighQuality,
          500, 500, _QualityThresholds.HighQuality));

        _Testee.ApplyObservations(new(2, _2s), GetObservations(
          150, 200, _QualityThresholds.HighQuality,
          200, 750, _QualityThresholds.HighQuality,
          700, 150, _QualityThresholds.HighQuality,
          700, 750, _QualityThresholds.HighQuality,
          550, 550, _QualityThresholds.HighQuality));

        var latest = _Testee.Latest!;
        Assert.NotNull(latest.Current);
        Assert.Equal(4, latest.OtherCandidates.Count());

        _Testee.ApplyObservations(new(2, _3s), []);

        latest = _Testee.Latest!;
        Assert.NotNull(latest.Current);
        Assert.Empty(latest.OtherCandidates);
    }

    private static List<ObservedBall> GetObservations(params double[] properties)
    {
        Assert.Equal(0, properties.Length % 3);
        List<ObservedBall> observations = [];

        for (int i = 0; i < properties.Length; i += 3)
        {
            ObservedBall ball = new(new Point(properties[i], properties[i + 1]), properties[i + 2]);
            observations.Add(ball);
        }

        return observations;
    }

    private static void Verify(
        TrackedBall? ball,
        int id,
        ulong frameId,
        long timestampNs,
        TrackingConfidence conf,
        TrackingStatus status)
    {
        Assert.NotNull(ball);
        Assert.Equal(id, ball.Id);
        Assert.Equal(frameId, ball.Frame.Id);
        Assert.Equal(timestampNs, ball.Frame.TimestampNs);
        Assert.Equal(conf, ball.Confidence);
        Assert.Equal(status, ball.Status);
    }
}
