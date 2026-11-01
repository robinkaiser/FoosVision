// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.CalibrationCore.Services;
using FoosVision.Domain.Game.Services.GameTracking;
using FoosVision.Domain.Game.Services.ReplayDecision;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Game.Entities;

public enum ChangeKind
{
    TrackedBallInfo,
    TrackedBallLostInfo,
    UpdateTableConfigRequest,
    UpdateTableSceneRequest,
    ReplayRequest,
    ReturnToLiveRequest,
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

public record ReturnToLiveRequest(Frame ReplayTriggerFrame) : Change(ChangeKind.ReturnToLiveRequest);

public record ReplayRequest(
    Frame AnchorFrame,
    Point AnchorPosition,
    BallPossession AnchorPossession,
    int AnchorPossessionTimeMs,
    ReplayTriggerKind TriggerKind) : Change(ChangeKind.ReplayRequest);

public class GameSession
{
    private const double _ReturnToLiveMovementThresholdPx = 10;
    // (1 s clip + about 0.25 s keyframe preroll) / 0.25 playback speed is about 5 s.
    // Round up for frame boundaries; this is a recorder-side delay, not a playback acknowledgement.
    private const long _ReturnToLiveMovementDelayNs = 6_000L * 1_000_000L;

    private readonly Lock _Gate = new();
    private readonly IGameTracker _GameTracker;
    private readonly TableUpdateSchedule _TableUpdates = new();
    private Frame? _ReplayTriggerFrame;
    private Point? _ReturnToLiveReferencePosition;
    private bool _ReturnToLiveRequested;

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

            var isTableConfigUpdateRequired = _TableUpdates.RequestTableUpdate(frame);

            if (isTableConfigUpdateRequired)
            {
                UpdateTableConfigRequest request = new();
                changes.Add(request);
            }

            var isTableSceneUpdateRequired = _TableUpdates.RequestTableSceneUpdate(
                frame, snapshot.IsBallFound, snapshot.BallPosition, snapshot.BallCandidates);

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
                _ReplayTriggerFrame = frame;
                _ReturnToLiveReferencePosition = null;
                _ReturnToLiveRequested = false;
            }
            else if (_ReplayTriggerFrame is Frame triggerFrame &&
                frame.TimestampNs - triggerFrame.TimestampNs >= _ReturnToLiveMovementDelayNs)
            {
                if (snapshot.IsBallFound && !_ReturnToLiveRequested)
                {
                    if (_ReturnToLiveReferencePosition is Point reference)
                    {
                        double dx = snapshot.BallPosition.X - reference.X;
                        double dy = snapshot.BallPosition.Y - reference.Y;
                        _ReturnToLiveRequested = (dx * dx) + (dy * dy) >= _ReturnToLiveMovementThresholdPx * _ReturnToLiveMovementThresholdPx;
                    }
                    else
                    {
                        _ReturnToLiveReferencePosition = snapshot.BallPosition;
                    }
                }

                if (_ReturnToLiveRequested)
                {
                    // Keep the decision available until superseded by another replay.
                    changes.Add(new ReturnToLiveRequest(triggerFrame));
                }
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
            _TableUpdates.CompleteTableUpdate();
        }
    }

    public void CompleteTableSceneUpdate()
    {
        lock (_Gate)
        {
            _TableUpdates.CompleteTableSceneUpdate();
        }
    }

    private static List<TrackedBall> GetCandidateTracks(GameTrackingSnapshot snapshot)
    {
        var candidates = snapshot.IsBallFound
            ? snapshot.BallCandidates.Where(candidate => candidate.Position != snapshot.BallPosition)
            : snapshot.BallCandidates;

        return [.. candidates];
    }
}
