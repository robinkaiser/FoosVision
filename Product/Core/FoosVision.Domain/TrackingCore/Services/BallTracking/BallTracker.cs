// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.BallTracking;

internal enum TrackedBallUpdateType
{
    None,
    Observed,
    Predicted,
}

internal record InternalBall(Ball TrackedBall, Point Prediction)
{
    public TrackedBallUpdateType UpdateType { get; set; } = TrackedBallUpdateType.None;
}

internal record InternalObservation(ObservedBall ObservedBall)
{
    public bool WasMatched { get; set; } = false;
}

internal record ReacquisitionAnchor(Frame LastObservedFrame, Point Position);

public class BallTracker : IBallTracker
{
    private static readonly Source _Log = new("BallTracker");

    private readonly BallTrackerParams _Params;
    private readonly Queue<Ball> _AvailableBalls;

    private BoundsChecker _BoundsChecker;
    private List<Ball> _TrackedBalls;
    private List<ReacquisitionAnchor> _ReacquisitionAnchors;
    private int _NextId;

    public BallTracker(BallTrackerParams parameters, TableConfiguration tableConfig)
    {
        _Params = parameters;
        _AvailableBalls = new();

        for (int i = 0; i < _Params.MaxTrackedBallsCount; i++)
        {
            var ball = new Ball(_Params.LowPassAlphaDeltaXY);
            _AvailableBalls.Enqueue(ball);
        }

        _BoundsChecker = new BoundsChecker(tableConfig.Field.Boundary);
        _TrackedBalls = [];
        _ReacquisitionAnchors = [];
    }

    public TrackingSnapshot? Latest { get; private set; }

    public TrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
    {
        long minReacquisitionTimeNs = (long)(frame.TimestampNs - _Params.TrackedBallReacquisitionTime.TotalNanoseconds);
        RetireOldReacquisitionAnchors(minReacquisitionTimeNs);

        long minUpdateTime = (long)(frame.TimestampNs - _Params.TrackedBallMaxUnobservedTime.TotalNanoseconds);
        RetireBallsWithLongUnobservedTime(minUpdateTime, minReacquisitionTimeNs);

        List<InternalBall> internalTracked = [];

        foreach (var ball in _TrackedBalls)
        {
            var prediction = ball.GetPrediction(frame);
            internalTracked.Add(new(ball, prediction));
        }

        List<InternalObservation> internalObservations = [];

        var usableObservations = observations
            .Where(o => o.QualityLevel != ObservationQualityLevel.BelowMinimum)
            .OrderByDescending(GetObservationEvidenceRank);

        foreach (var observation in usableObservations)
        {
            internalObservations.Add(new(observation));
        }

        if (internalTracked.Count > 0)
        {
            ProcessTrackedBalls(frame, internalTracked, internalObservations);
        }

        if (internalObservations.Count > 0)
        {
            var unmatchedObservations = internalObservations.Where(o => !o.WasMatched);
            ProcessUnmatchedObservations(frame, internalTracked, unmatchedObservations);
        }

        // Perform a stable sort of tracked balls, so that balls that were once higher ranked due to
        // stronger observation evidence stay higher ranked because they should be the better guess.
        _TrackedBalls = [.. _TrackedBalls.OrderByDescending(b => GetEvidenceRank(b.Evidence))];

        RetireBallsWithLongPredictionDistance(_Params.TrackedBallMaxPredictionDistancePx);
        RetireBallsOutsideOfPlayingField();

        var trackedBall = _TrackedBalls.FirstOrDefault();

        if (trackedBall == null)
        {
            Latest = new(frame, []);
            return Latest;
        }

        if (GetEvidenceRank(trackedBall.Evidence) >= GetEvidenceRank(TrackingEvidence.HighQualityObservation))
        {
            RetireBallsWithWeakEvidence(TrackingEvidence.HighQualityObservation);
        }

        Latest = new(frame, [.. _TrackedBalls.Select(CreateTrackedBall)]);

        return Latest;
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        _BoundsChecker = new BoundsChecker(tableConfig.Field.Boundary);
    }

    private static double GetDistance(Point prediction, ObservedBall observation)
        => GetDistance(prediction, observation.Position);

    private static double GetDistance(Point p0, Point p1)
    {
        double dX = p0.X - p1.X;
        double dY = p0.Y - p1.Y;
        double distance = Math.Sqrt((dX * dX) + (dY * dY));
        return distance;
    }

    private TrackedBall CreateTrackedBall(Ball b)
    {
        var trackingStatus = b.UpdateType switch
        {
            TrackedBallUpdateType.Predicted => TrackingStatus.Predicted,
            _ => TrackingStatus.Observed,
        };

        return new(
            b.Id,
            b.LastProcess,
            new Point(b.X, b.Y),
            GetConfidenceFromEvidence(b.Evidence),
            trackingStatus,
            b.VelocityPxPerS)
        {
            Evidence = b.Evidence,
            UnobservedAgeMs = GetUnobservedAgeMs(b),
        };
    }

    private static int GetUnobservedAgeMs(Ball b)
    {
        var ageNs = Math.Max(0, b.LastProcess.TimestampNs - b.LastUpdate.TimestampNs);

        return (int)(ageNs / 1_000_000);
    }

    private static int GetEvidenceRank(TrackingEvidence evidence)
    {
        return evidence switch
        {
            TrackingEvidence.VeryHighQualityObservation => 3,
            TrackingEvidence.HighQualityObservation => 2,
            TrackingEvidence.LowQualityObservation => 1,
            _ => 0,
        };
    }

    private static int GetObservationEvidenceRank(ObservedBall observedBall)
        => GetObservationQualityRank(observedBall.QualityLevel);

    private static int GetObservationQualityRank(ObservationQualityLevel qualityLevel)
    {
        return qualityLevel switch
        {
            ObservationQualityLevel.VeryHighQuality => 3,
            ObservationQualityLevel.HighQuality => 2,
            ObservationQualityLevel.LowQuality => 1,
            _ => 0,
        };
    }

    private static TrackingEvidence GetEvidenceFromObservation(ObservedBall observedBall)
    {
        return observedBall.QualityLevel switch
        {
            ObservationQualityLevel.VeryHighQuality => TrackingEvidence.VeryHighQualityObservation,
            ObservationQualityLevel.HighQuality => TrackingEvidence.HighQualityObservation,
            _ => TrackingEvidence.LowQualityObservation,
        };
    }

    private static TrackingConfidence GetConfidenceFromEvidence(TrackingEvidence evidence)
    {
        return evidence switch
        {
            TrackingEvidence.VeryHighQualityObservation => TrackingConfidence.High,
            TrackingEvidence.HighQualityObservation => TrackingConfidence.Average,
            _ => TrackingConfidence.Low,
        };
    }

    private void ProcessTrackedBalls(Frame frame, List<InternalBall> balls, IList<InternalObservation> observations)
    {
        foreach (var observation in observations)
        {    // Each observation updates the nearest tracked ball
            var trackedBallsNotUpdated = balls.Where(b => b.UpdateType == TrackedBallUpdateType.None);
            if (!trackedBallsNotUpdated.Any()) continue;

            var observedBall = observation.ObservedBall;
            var nearestTrackedBall = trackedBallsNotUpdated.MinBy(t => GetDistance(t.Prediction, observedBall));
            if (nearestTrackedBall == null) continue;

            var distance = GetDistance(nearestTrackedBall.Prediction, observedBall);

            var isVeryNearBy = distance <= _Params.TrackedBallMaxNearByDistanceForObservationMatchingPx;
            var isQuiteNearBy = distance <= _Params.TrackedBallMaxQuiteNearByDistanceForObservationMatchingPx;
            var wasNotUpdatedByAnObservationYet = !nearestTrackedBall.TrackedBall.WasUpdatedByObservationAtLeastOnce;

            var isMatched = isVeryNearBy || (isQuiteNearBy && wasNotUpdatedByAnObservationYet);

            if (!isMatched) continue;

            var (x, y) = observedBall.Position;
            nearestTrackedBall.TrackedBall.Update(frame, x, y, observedBall.Quality, GetEvidenceFromObservation(observedBall));

            // Take both tracked ball and observation out of the equation
            nearestTrackedBall.UpdateType = TrackedBallUpdateType.Observed;
            observation.WasMatched = true;
        }

        // All remaining tracked balls without any observation (very) near by are predicted
        var trackedBallsStillNotUpdated = balls.Where(b => b.UpdateType == TrackedBallUpdateType.None);

        foreach (var trackedBall in trackedBallsStillNotUpdated)
        {
            trackedBall.TrackedBall.Predict(frame);
            trackedBall.UpdateType = TrackedBallUpdateType.Predicted;
        }
    }

    private void ProcessUnmatchedObservations(Frame frame, List<InternalBall> balls, IEnumerable<InternalObservation> unmatchedObservations)
    {
        // Create new tracked balls from unmatched observations
        foreach (var observation in unmatchedObservations)
        {
            var ball = observation.ObservedBall;

            if (ball.QualityLevel >= ObservationQualityLevel.HighQuality)
            {   // High quality observations always lead to tracked ball creation
                AddTrackedBall(frame, ball.Position, GetEvidenceFromObservation(ball));
            }
            else if (IsNearTrackedBallPrediction(balls, observation.ObservedBall) ||
                     IsNearReacquisitionAnchor(observation.ObservedBall))
            {   // Low quality observations can restart near a visible prediction or a recent invisible anchor
                AddTrackedBall(frame, ball.Position, GetEvidenceFromObservation(ball));
            }
        }
    }

    private bool IsNearTrackedBallPrediction(List<InternalBall> balls, ObservedBall observation)
    {
        if (balls.Count == 0)
        {
            return false;
        }

        var trackedBallNearBy = balls.MinBy(c => GetDistance(c.Prediction, observation));
        if (trackedBallNearBy == null)
        {
            return false;
        }

        var distance = GetDistance(trackedBallNearBy.Prediction, observation);

        return distance <= _Params.TrackedBallMaxDistanceForCreationBasedOnLowQObservationPx;
    }

    private bool IsNearReacquisitionAnchor(ObservedBall observation)
    {
        var anchorNearBy = _ReacquisitionAnchors.MinBy(a => GetDistance(a.Position, observation.Position));

        if (anchorNearBy == null)
        {
            return false;
        }

        var distance = GetDistance(anchorNearBy.Position, observation.Position);

        return distance <= _Params.TrackedBallMaxReacquisitionDistancePx;
    }

    private bool AddTrackedBall(Frame frame, Point position, TrackingEvidence evidence)
    {
        if (_AvailableBalls.Count == 0)
        {
            _Log.Information($"AddTrackedBall - Too many parallel tracked balls, discard ({position})");
            return false;
        }

        var ball = _AvailableBalls.Dequeue();
        ball.Initialize(frame, _NextId, position.X, position.Y, evidence);
        _TrackedBalls.Add(ball);
        RemoveReacquisitionAnchorsNear(position);

        _NextId++;

        if (_NextId > 999)
        {
            _NextId = 1;
        }

        return true;
    }

    private void RetireBallsWithWeakEvidence(TrackingEvidence minEvidence)
    {
        var minRank = GetEvidenceRank(minEvidence);
        var balls = _TrackedBalls.Where(c => GetEvidenceRank(c.Evidence) < minRank);

        RetireBalls(balls);
    }

    private void RetireBallsWithLongPredictionDistance(double maxPredictionDistance)
    {
        var balls = _TrackedBalls.Where(c => c.DistancePredicted > maxPredictionDistance);

        RetireBalls(balls);
    }

    private void RetireBallsWithLongUnobservedTime(long minUpdateTimeNs, long minReacquisitionTimeNs)
    {
        var balls = _TrackedBalls.Where(c => c.LastUpdate.TimestampNs < minUpdateTimeNs);

        RetireBalls(balls, minReacquisitionTimeNs);
    }

    private void RetireOldReacquisitionAnchors(long minObservedTimeNs)
    {
        _ReacquisitionAnchors = [.. _ReacquisitionAnchors.Where(a => a.LastObservedFrame.TimestampNs >= minObservedTimeNs)];
    }

    private void AddReacquisitionAnchor(Ball ball)
    {
        var position = new Point(ball.X, ball.Y);

        if (_BoundsChecker.IsOutside(position))
        {
            return;
        }

        _ReacquisitionAnchors.Add(new(ball.LastUpdate, position));
    }

    private void RemoveReacquisitionAnchorsNear(Point position)
    {
        var anchors = _ReacquisitionAnchors
            .Where(anchor => GetDistance(anchor.Position, position) <= _Params.TrackedBallMaxReacquisitionDistancePx)
            .ToList();

        foreach (var anchor in anchors)
        {
            _ReacquisitionAnchors.Remove(anchor);
        }
    }

    private void RetireBallsOutsideOfPlayingField()
    {
        var balls = _TrackedBalls.Where(c => _BoundsChecker.IsOutside(new Point(c.X, c.Y)));

        RetireBalls(balls);
    }

    private void RetireBalls(IEnumerable<Ball> balls)
        => RetireBalls(balls, minReacquisitionTimeNs: null);

    private void RetireBalls(IEnumerable<Ball> balls, long? minReacquisitionTimeNs)
    {
        var ballsToBeRetired = balls.ToList();

        foreach (var ball in ballsToBeRetired)
        {
            if (minReacquisitionTimeNs.HasValue &&
                ball.LastUpdate.TimestampNs >= minReacquisitionTimeNs.Value)
            {
                AddReacquisitionAnchor(ball);
            }

            _AvailableBalls.Enqueue(ball);
            _TrackedBalls.Remove(ball);
        }
    }
}
