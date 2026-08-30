// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.Services.Possession;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Replay.Entities;

public readonly record struct ReplayId(ulong TriggerFrameId, long TriggerTimestampNs);

public class ReplaySession
{
    public const int DefaultRequiredCompletedLoops = 1;
    private const int _BallSearchRegionRadiusPx = 192;
    private const double _ReturnToLiveMovementThresholdPx = 10.0;

    private readonly IBallTracker _BallTracker;
    private readonly IReplayAnalyzer _ReplayAnalyzer;
    private readonly List<ReplayTrackedFrame> _TrackedFrames = [];
    private ReplayTrackAnchor? _TrackAnchor;
    private PossessionCalculator? _PossessionCalculator;
    private Point? _ReturnToLiveReferencePosition;
    private Point _LastKnownPosition;

    public ReplaySession(
        IBallTracker ballTracker,
        IReplayAnalyzer replayAnalyzer,
        int requiredCompletedLoops = DefaultRequiredCompletedLoops)
    {
        requiredCompletedLoops = Math.Max(requiredCompletedLoops, 1);

        _BallTracker = ballTracker;
        _ReplayAnalyzer = replayAnalyzer;
        RequiredCompletedLoops = requiredCompletedLoops;
    }

    public Option<ReplayId> CurrentReplayId { get; private set; } = Option<ReplayId>.None();

    public Option<TableConfiguration> TableConfiguration { get; private set; } = Option<TableConfiguration>.None();

    public int CompletedLoops { get; private set; }

    public int RequiredCompletedLoops { get; }

    public int TrackedFrameCount => _TrackedFrames.Count;

    public bool IsActive => CurrentReplayId.IsSome;

    public bool HasCompletedRequiredLoops => CompletedLoops >= RequiredCompletedLoops;

    public ReplayTrackedFrame Start(ReplayId replayId, ReplayTrackAnchor trackAnchor, TableConfiguration tableConfiguration)
    {
        CurrentReplayId = replayId;
        TableConfiguration = Option<TableConfiguration>.Some(tableConfiguration);
        CompletedLoops = 0;
        _ReturnToLiveReferencePosition = null;
        _TrackAnchor = trackAnchor;
        _PossessionCalculator = new(tableConfiguration);
        _LastKnownPosition = trackAnchor.Position;
        _TrackedFrames.Clear();

        _BallTracker.ApplyObservations(
            trackAnchor.Frame,
            [new ObservedBall(trackAnchor.Position, 1.0)]);

        var possession = _PossessionCalculator.Compute(trackAnchor.Position);

        ReplayTrackedFrame anchorFrame = new(
            trackAnchor.Frame.TimestampNs,
            trackAnchor.Position,
            possession,
            ReplayTrackedFrameStatus.Anchor,
            Vector2.Zero);

        _TrackedFrames.Add(anchorFrame);
        return anchorFrame;
    }

    public ReplayTrackedFrame ApplyObservations(Frame frame, IReadOnlyList<ObservedBall> observations)
    {
        if (!IsActive)
        {
            throw new InvalidOperationException("Replay session is not active.");
        }

        if (!CanApplyObservations(frame))
        {
            throw new InvalidOperationException("Replay frame is not after the track anchor.");
        }

        TrackingSnapshot snapshot = _BallTracker.ApplyObservations(frame, observations);
        TrackedBall? trackedBall = snapshot.Current;

        if (trackedBall == null)
        {
            ReplayTrackedFrame missingFrame = new(
                frame.TimestampNs,
                _LastKnownPosition,
                BallPossession.None,
                ReplayTrackedFrameStatus.Missing,
                Vector2.Zero);

            _TrackedFrames.Add(missingFrame);
            return missingFrame;
        }

        _LastKnownPosition = trackedBall.Position;

        var possession = _PossessionCalculator != null ?
            _PossessionCalculator.Compute(trackedBall.Position) :
            BallPossession.None;

        ReplayTrackedFrameStatus status = trackedBall.Status == TrackingStatus.Observed
            ? ReplayTrackedFrameStatus.Tracked
            : ReplayTrackedFrameStatus.Predicted;

        ReplayTrackedFrame trackedFrame = new(
            frame.TimestampNs,
            trackedBall.Position,
            possession,
            status,
            trackedBall.VelocityPxPerS);

        _TrackedFrames.Add(trackedFrame);
        return trackedFrame;
    }

    public ReplayAnalysis GetAnalysis()
    {
        return _ReplayAnalyzer.Analyze(_TrackedFrames);
    }

    public bool CanApplyObservations(Frame frame)
    {
        return IsActive &&
            _TrackAnchor != null &&
            frame.TimestampNs > _TrackAnchor.Frame.TimestampNs;
    }

    public Rectangle GetBallSearchRegion()
    {
        const int Diameter = _BallSearchRegionRadiusPx * 2;
        int x = (int)Math.Round(_LastKnownPosition.X, MidpointRounding.AwayFromZero) - _BallSearchRegionRadiusPx;
        int y = (int)Math.Round(_LastKnownPosition.Y, MidpointRounding.AwayFromZero) - _BallSearchRegionRadiusPx;
        return new Rectangle(x, y, Diameter, Diameter);
    }

    public void CompleteLoop()
    {
        if (!IsActive)
        {
            return;
        }

        CompletedLoops++;
    }

    public bool CanReturnToLive(Point? liveBallPosition)
    {
        if (!IsActive ||
            !HasCompletedRequiredLoops ||
            liveBallPosition == null)
        {
            return false;
        }

        if (_ReturnToLiveReferencePosition == null)
        {
            _ReturnToLiveReferencePosition = liveBallPosition.Value;
            return false;
        }

        return GetDistance(_ReturnToLiveReferencePosition.Value, liveBallPosition.Value) >= _ReturnToLiveMovementThresholdPx;
    }

    public void Stop()
    {
        CurrentReplayId = Option<ReplayId>.None();
        TableConfiguration = Option<TableConfiguration>.None();
        CompletedLoops = 0;
        _ReturnToLiveReferencePosition = null;
        _TrackAnchor = null;
        _PossessionCalculator = null;
        _TrackedFrames.Clear();
    }

    private static double GetDistance(Point a, Point b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
