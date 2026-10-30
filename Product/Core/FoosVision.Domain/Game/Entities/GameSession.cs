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
    private readonly Lock _Gate = new();
    private readonly IGameTracker _GameTracker;
    private readonly TableUpdateSchedule _TableUpdates = new();

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
