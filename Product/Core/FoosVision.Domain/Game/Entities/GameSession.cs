// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.GameTracking;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Game.Entities;

public enum ChangeKind
{
    TrackedBallInfo,
    TrackedBallLostInfo,
    UpdateTableConfigRequest,
    UpdateTableSceneRequest,
    ReplayRequest,
}

public abstract record Change(ChangeKind Kind);

public record TrackedBallInfo(
    bool IsFound,
    BallPossession Possession,
    int PossessionTimeMs,
    bool IsTimeFoul,
    Point Position,
    Vector2 VelocityPxPerS,
    IReadOnlyList<TrackedBall> Candidates) : Change(ChangeKind.TrackedBallInfo);

public record UpdateTableConfigRequest() : Change(ChangeKind.UpdateTableConfigRequest);

public record UpdateTableSceneRequest() : Change(ChangeKind.UpdateTableSceneRequest);

public record ReplayRequest(
    Frame AnchorFrame,
    Point AnchorPosition,
    BallPossession AnchorPossession,
    int AnchorPossessionTimeMs,
    ReplayTriggerKind TriggerKind) : Change(ChangeKind.ReplayRequest);

public class GameSession
{
    private const long _TableConfigUpdateInterval_ns = 10000L * 1_000_000L;
    private const long _TableSceneUpdateInterval_ns = 500L * 1_000_000L;
    private const int _TableSceneUpdateMinDistance_px = 50;

    private readonly Lock _Gate = new();
    private readonly IGameTracker _GameTracker;

    private Frame _LastTableConfigUpdateFrame;
    private bool _TableUpdateInProgress;

    private Frame _LastTableSceneUpdateFrame;
    private Point _LastTableSceneUpdatePosition;
    private bool _TableSceneUpdateInProgress;

    public GameSession(
        Guid id,
        IGameTracker gameTracker,
        TableConfiguration tableConfig)
    {
        Id = id;
        _GameTracker = gameTracker;
        TableConfig = tableConfig;
    }

    public Guid Id { get; }

    public TableConfiguration TableConfig { get; private set; }

    public IReadOnlyList<Change> ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
    {
        lock (_Gate)
        {
            var snapshot = _GameTracker.ApplyObservations(frame, observations);
            var candidates = GetCandidateTracks(snapshot);

            TrackedBallInfo info = new(
                snapshot.IsBallFound,
                snapshot.Possession,
                snapshot.PossessionTimeMs,
                snapshot.IsTimeFoul,
                snapshot.BallPosition,
                snapshot.BallVelocityPxPerS,
                candidates);
            var changes = new List<Change>() { info };

            var isTableConfigUpdateRequired = IsTableUpdateRequired(frame);

            if (isTableConfigUpdateRequired)
            {
                UpdateTableConfigRequest request = new();
                changes.Add(request);
            }

            var isTableSceneUpdateRequired = IsTableSceneUpdateRequired(frame, snapshot);

            if (isTableSceneUpdateRequired)
            {
                UpdateTableSceneRequest request = new();
                changes.Add(request);
            }

            if (snapshot.IsReplaySuggested &&
                snapshot.ReplayAnchor != null)
            {
                ReplayRequest check = new(
                    snapshot.ReplayAnchor.Frame,
                    snapshot.ReplayAnchor.Position,
                    snapshot.ReplayAnchor.Possession,
                    snapshot.ReplayAnchor.PossessionTimeMs,
                    snapshot.ReplayAnchor.TriggerKind);
                changes.Add(check);
            }

            return changes;
        }
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        lock (_Gate)
        {
            _GameTracker.UpdateTableConfig(tableConfig);
            TableConfig = tableConfig;
        }
    }

    public void CompleteTableUpdate()
    {
        lock (_Gate)
        {
            _TableUpdateInProgress = false;
        }
    }

    public void CompleteTableSceneUpdate()
    {
        lock (_Gate)
        {
            _TableSceneUpdateInProgress = false;
        }
    }

    private bool IsTableUpdateRequired(Frame frame)
    {
        if (HasCalibrationUpdateInProgress) return false;

        var isDue = (frame.TimestampNs - _LastTableConfigUpdateFrame.TimestampNs) >= _TableConfigUpdateInterval_ns;
        if (!isDue) return false;

        _LastTableConfigUpdateFrame = frame;
        _TableUpdateInProgress = true;

        return true;
    }

    private bool IsTableSceneUpdateRequired(Frame frame, GameTrackingSnapshot snapshot)
    {
        if (HasCalibrationUpdateInProgress) return false;
        if (!TryGetFoundBall(snapshot, out TrackedBall? trackedBall)) return false;

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

    private static List<TrackedBall> GetCandidateTracks(GameTrackingSnapshot snapshot)
    {
        var candidates = snapshot.IsBallFound
            ? snapshot.BallCandidates.Where(candidate => candidate.Position != snapshot.BallPosition)
            : snapshot.BallCandidates;

        return [.. candidates];
    }

    private static bool TryGetFoundBall(GameTrackingSnapshot snapshot, [NotNullWhen(true)] out TrackedBall? trackedBall)
    {
        trackedBall = snapshot.BallCandidates.FirstOrDefault(candidate =>
            candidate.Status == TrackingStatus.Observed &&
            candidate.Position == snapshot.BallPosition);

        return
            snapshot.IsBallFound &&
            trackedBall != null;
    }

    private bool HasCalibrationUpdateInProgress =>
        _TableUpdateInProgress ||
        _TableSceneUpdateInProgress;
}
