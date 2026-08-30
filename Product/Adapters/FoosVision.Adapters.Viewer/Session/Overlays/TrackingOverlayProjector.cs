// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.Session.Overlays;

internal class TrackingOverlayProjector
{
    private const float _FrameWidth = 1920f;
    private const float _FrameHeight = 1080f;
    private const long _TrailWindowNs = 1_000_000_000L;
    private const long _MetricHoldDurationNs = 500_000_000L;
    private const double _MetricHoldThresholdKmh = 3.0;
    private readonly Queue<TrailSample> _Samples = [];
    private TableImageScale? _TableImageScale;
    private double _HeldGoalSpeedKmh;
    private double _HeldSideSpeedKmh;
    private long? _MetricsHoldUntilTimestampNs;
    private ulong? _LastFrameId;
    private long? _LastTimestampNs;

    public void UpdateTableConfiguration(TableConfiguration tableConfiguration)
    {
        _TableImageScale = TableImageScale.From(tableConfiguration);
        ResetMetrics();
    }

    public void Reset()
    {
        _Samples.Clear();
        ResetMetrics();
        _LastFrameId = null;
        _LastTimestampNs = null;
    }

    public TrackingOverlayState Project(TrackingFrameMessage message)
    {
        if ((_LastTimestampNs.HasValue && message.TimestampNs < _LastTimestampNs.Value) ||
            (_LastFrameId.HasValue && message.FrameId < _LastFrameId.Value))
        {
            Reset();
        }

        if (message.IsBallFound && message.BallPosition is not null)
        {
            _Samples.Enqueue(new TrailSample(message.TimestampNs, CreatePoint(message.BallPosition)));
        }

        long cutoffTimestampNs = message.TimestampNs - _TrailWindowNs;
        while (_Samples.Count > 0 && _Samples.Peek().TimestampNs < cutoffTimestampNs)
        {
            _Samples.Dequeue();
        }

        BallPossession possession = ParsePossession(message.Possession);
        OverlayPoint? ballPosition = message.IsBallFound && message.BallPosition is not null
            ? CreatePoint(message.BallPosition)
            : null;
        IReadOnlyList<ObservationOverlayPoint> observations = [.. message.Observations.Select(CreateObservation)];
        IReadOnlyList<BallCandidateOverlayPoint> ballCandidates = [.. message.BallCandidates.Select(CreateBallCandidate)];
        IReadOnlyList<TrackingOverlayMetric> metrics = CreateMetrics(message);

        _LastTimestampNs = message.TimestampNs;
        _LastFrameId = message.FrameId;

        return new TrackingOverlayState(
            Trail: [.. _Samples.Select(sample => sample.Point)],
            BallPosition: ballPosition,
            Observations: observations,
            BallCandidates: ballCandidates,
            PossessingTeam: possession.Team,
            PossessionArea: possession.Area,
            PossessionTimeMs: message.PossessionTimeMs,
            IsTimeFoul: message.IsTimeFoul,
            Metrics: metrics);
    }

    private IReadOnlyList<TrackingOverlayMetric> CreateMetrics(TrackingFrameMessage message)
    {
        if (_TableImageScale is not TableImageScale scale)
        {
            return [];
        }

        double goalSpeedKmh = scale.ConvertGoalAxisSpeedPxPerSToKmh(Math.Abs(message.BallVelocityPxPerS.X));
        double sideSpeedKmh = scale.ConvertSideAxisSpeedPxPerSToKmh(Math.Abs(message.BallVelocityPxPerS.Y));
        (goalSpeedKmh, sideSpeedKmh) = ApplyMetricsHold(message.TimestampNs, goalSpeedKmh, sideSpeedKmh);

        return
        [
            new(
                ReplayAnalyzer.GoalSpeedMetricName,
                goalSpeedKmh,
                "km/h"),
            new(
                ReplayAnalyzer.SideSpeedMetricName,
                sideSpeedKmh,
                "km/h"),
        ];
    }

    private (double GoalSpeedKmh, double SideSpeedKmh) ApplyMetricsHold(
        long timestampNs,
        double goalSpeedKmh,
        double sideSpeedKmh)
    {
        bool isAboveThreshold =
            goalSpeedKmh > _MetricHoldThresholdKmh ||
            sideSpeedKmh > _MetricHoldThresholdKmh;
        bool isHoldActive =
            _MetricsHoldUntilTimestampNs.HasValue &&
            timestampNs <= _MetricsHoldUntilTimestampNs.Value;

        if (isAboveThreshold)
        {
            _HeldGoalSpeedKmh = isHoldActive
                ? Math.Max(_HeldGoalSpeedKmh, goalSpeedKmh)
                : goalSpeedKmh;
            _HeldSideSpeedKmh = isHoldActive
                ? Math.Max(_HeldSideSpeedKmh, sideSpeedKmh)
                : sideSpeedKmh;
            _MetricsHoldUntilTimestampNs = timestampNs + _MetricHoldDurationNs;

            return (_HeldGoalSpeedKmh, _HeldSideSpeedKmh);
        }

        if (isHoldActive)
        {
            return (_HeldGoalSpeedKmh, _HeldSideSpeedKmh);
        }

        ResetMetrics();
        return (goalSpeedKmh, sideSpeedKmh);
    }

    private void ResetMetrics()
    {
        _HeldGoalSpeedKmh = 0.0;
        _HeldSideSpeedKmh = 0.0;
        _MetricsHoldUntilTimestampNs = null;
    }

    private static BallPossession ParsePossession(PossessionMessage value)
    {
        Team team = value.Team switch
        {
            TeamMessage.A => Team.A,
            TeamMessage.B => Team.B,
            _ => Team.None,
        };

        PossessionArea area = value.Area switch
        {
            PossessionAreaMessage.Defense => PossessionArea.Defense,
            PossessionAreaMessage.FiveBar => PossessionArea.FiveBar,
            PossessionAreaMessage.ThreeBar => PossessionArea.ThreeBar,
            _ => PossessionArea.None,
        };

        return new BallPossession(team, area);
    }

    private static OverlayPoint CreatePoint(PointMessage message)
    {
        return new OverlayPoint(
            X: Math.Clamp((float)message.X / _FrameWidth, 0f, 1f),
            Y: Math.Clamp((float)message.Y / _FrameHeight, 0f, 1f));
    }

    private static ObservationOverlayPoint CreateObservation(ObservedBallMessage message)
    {
        return new ObservationOverlayPoint(
            CreatePoint(message.Position),
            message.QualityLevel switch
            {
                ObservationQualityLevelMessage.VeryHighQuality => ObservationQualityLevel.VeryHighQuality,
                ObservationQualityLevelMessage.HighQuality => ObservationQualityLevel.HighQuality,
                ObservationQualityLevelMessage.LowQuality => ObservationQualityLevel.LowQuality,
                _ => ObservationQualityLevel.BelowMinimum,
            });
    }

    private static BallCandidateOverlayPoint CreateBallCandidate(TrackedBallCandidateMessage message)
    {
        return new BallCandidateOverlayPoint(
            CreatePoint(message.Position),
            message.Status switch
            {
                TrackingStatusMessage.Predicted => TrackingStatus.Predicted,
                _ => TrackingStatus.Observed,
            },
            message.Confidence switch
            {
                TrackingConfidenceMessage.High => TrackingConfidence.High,
                TrackingConfidenceMessage.Average => TrackingConfidence.Average,
                _ => TrackingConfidence.Low,
            });
    }

    private readonly record struct TrailSample(long TimestampNs, OverlayPoint Point);
}
