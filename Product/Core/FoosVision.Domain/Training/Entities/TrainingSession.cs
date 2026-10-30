// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.CalibrationCore.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.Services;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Entities;

public record TrainingFrameResult(
    LiveTrackingSnapshot Tracking,
    IReadOnlyList<TrainingEvent> Events,
    bool RequestTableConfigUpdate,
    bool RequestTableSceneUpdate,
    TrainingState State,
    TrainingAttempt? Attempt,
    TrainingVariant? Variant);

public class TrainingSession
{
    public const int ExecutionTimeoutMs = 3000;

    private readonly Lock _Gate = new();
    private readonly LiveTracker _LiveTracker;
    private readonly ITrainingRandom _Random;
    private readonly IReadOnlyList<ITrainingStrategy> _Strategies;
    private readonly TableUpdateSchedule _TableUpdates = new();
    private ITrainingStrategy? _ActiveStrategy;
    private TrainingAttempt? _Attempt;
    private TrainingVariant? _Variant;
    private long _AttemptId;
    private long _AnnouncementId;
    private long? _LastTimestampNs;
    private long _ReadyTimestampNs;
    private long _WaitNs;
    private long _VariantTimestampNs;

    public TrainingSession(Guid id, LiveTracker liveTracker, TableConfiguration tableConfig, TrainingOptions options, ITrainingRandom random)
    {
        Id = id;
        _LiveTracker = liveTracker;
        TableConfig = tableConfig;
        Options = options;
        _Random = random;
        _Strategies = TrainingStrategyFactory.Create(options, tableConfig);
    }

    public Guid Id { get; }
    public TrainingOptions Options { get; }
    public TableConfiguration TableConfig { get; private set; }
    public TrainingState State { get; private set; } = TrainingState.Searching;
    public TrainingAttempt? Attempt => _Attempt;

    public TrainingFrameResult ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
    {
        lock (_Gate)
        {
            if (State == TrainingState.Stopped || IsStale(frame))
            {
                return Result(_LiveTracker.Latest ?? EmptySnapshot(frame), [], false, false);
            }

            LiveTrackingSnapshot snapshot = _LiveTracker.ApplyObservations(frame, observations);
            return ApplyTracking(snapshot);
        }
    }

    /// <summary>Consumes the monotonic facts of this session; all deadlines use the supplied frame time.</summary>
    public TrainingFrameResult ApplyTracking(LiveTrackingSnapshot snapshot)
    {
        lock (_Gate)
        {
            if (State == TrainingState.Stopped || IsStale(snapshot.Frame))
            {
                return Result(snapshot, [], false, false);
            }

            _LastTimestampNs = snapshot.Frame.TimestampNs;
            List<TrainingEvent> events = [];
            if (_ActiveStrategy is null)
            {
                State = TrainingState.Searching;
                FindPreparation(snapshot, events);
            }
            else
            {
                AdvanceAttempt(snapshot, events);
            }

            bool tableUpdate = _TableUpdates.RequestTableUpdate(snapshot.Frame);
            bool sceneUpdate = _TableUpdates.RequestTableSceneUpdate(snapshot.Frame, snapshot.IsBallFound, snapshot.BallPosition, snapshot.BallCandidates);
            return Result(snapshot, events.AsReadOnly(), tableUpdate, sceneUpdate);
        }
    }

    public IReadOnlyList<TrainingEvent> Stop()
    {
        lock (_Gate)
        {
            if (State == TrainingState.Stopped)
            {
                return [];
            }

            State = TrainingState.Stopped;
            ResetStrategies();
            _ActiveStrategy = null;
            _TableUpdates.CompleteTableUpdate();
            _TableUpdates.CompleteTableSceneUpdate();
            return _Attempt is null ? [] : [new TrainingAnnouncementsInvalidated(_Attempt, _AnnouncementId)];
        }
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        lock (_Gate)
        {
            if (State == TrainingState.Stopped)
            {
                return;
            }

            TableConfig = tableConfig;
            _LiveTracker.UpdateTableConfig(tableConfig);
            foreach (ITrainingStrategy strategy in _Strategies)
            {
                strategy.UpdateTableConfig(tableConfig);
            }
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

    private void FindPreparation(LiveTrackingSnapshot snapshot, List<TrainingEvent> events)
    {
        ITrainingStrategy? selected = null;
        TrainingPreparation? preparation = null;
        foreach (ITrainingStrategy strategy in _Strategies)
        {
            TrainingPreparation? candidate = strategy.FindPreparation(snapshot);
            if (selected is null && candidate is not null)
            {
                selected = strategy;
                preparation = candidate;
            }
        }

        if (selected is null || preparation is null)
        {
            return;
        }

        if (_Attempt is not null)
        {
            events.Add(new TrainingAnnouncementsInvalidated(_Attempt, _AnnouncementId));
        }

        _Attempt = new(Id, ++_AttemptId, selected.Options.Kind, preparation.Team, preparation.Bar, preparation.Anchor);
        _ActiveStrategy = selected;
        selected.Begin(preparation);
        _Variant = null;
        _ReadyTimestampNs = snapshot.Frame.TimestampNs;
        _WaitNs = (long)selected.Options.Wait.Select(_Random.NextUnit()).TotalNanoseconds;
        State = TrainingState.Waiting;
        events.Add(new TrainingPreparationConfirmed(_Attempt));
        Announce(events, TrainingAnnouncementKind.Ready, selected.ReadyText);
    }

    private void AdvanceAttempt(LiveTrackingSnapshot snapshot, List<TrainingEvent> events)
    {
        ITrainingStrategy strategy = _ActiveStrategy!;
        TrainingExecution execution = strategy.Evaluate(snapshot);
        if (execution.Anchor is not null)
        {
            _Attempt = _Attempt! with { Anchor = execution.Anchor };
        }

        if (_Variant is not null && execution.MovementFrame.HasValue &&
            execution.MovementFrame.Value.TimestampNs - _VariantTimestampNs > ExecutionTimeoutMs * 1_000_000L)
        {
            Abort(events, TrainingAbortReason.ExecutionTimeout);
            return;
        }

        if (execution.State == TrainingExecutionState.Completed)
        {
            if (_Variant is null)
            {
                Abort(events, TrainingAbortReason.PrematureExecution);
                return;
            }

            events.Add(new TrainingAttemptCompleted(_Attempt!, _Variant, execution.ConfirmationFrame!.Value, snapshot.Frame));
            Finish(TrainingState.Completed);
            return;
        }

        if (execution.State == TrainingExecutionState.Lost)
        {
            Abort(events, snapshot.Phase == LiveTrackingPhase.BallLost ? TrainingAbortReason.BallLost : TrainingAbortReason.PreparationLost);
            return;
        }

        if (execution.State == TrainingExecutionState.Pending)
        {
            State = TrainingState.Evaluating;
            return;
        }

        if (_Variant is not null && snapshot.Frame.TimestampNs - _VariantTimestampNs >= ExecutionTimeoutMs * 1_000_000L)
        {
            Abort(events, TrainingAbortReason.ExecutionTimeout);
            return;
        }

        // No observations means no announcement. Tracking already owns temporary ball loss.
        if (State == TrainingState.Waiting && snapshot.IsBallFound && snapshot.Frame.TimestampNs - _ReadyTimestampNs >= _WaitNs)
        {
            double unit = _Random.NextUnit();
            TrainingWaitOptions.ValidateUnit(unit);
            _Variant = strategy.Variants[(int)(unit * strategy.Variants.Count)];
            _VariantTimestampNs = snapshot.Frame.TimestampNs;
            State = TrainingState.AwaitingExecution;
            Announce(events, TrainingAnnouncementKind.Variant, _Variant.Text, _Variant);
        }
    }

    private void Abort(List<TrainingEvent> events, TrainingAbortReason reason)
    {
        events.Add(new TrainingAnnouncementsInvalidated(_Attempt!, _AnnouncementId));
        events.Add(new TrainingAttemptAborted(_Attempt!, reason));
        Announce(events, TrainingAnnouncementKind.Aborted, "Aborted");
        Finish(TrainingState.Aborted);
    }

    private void Finish(TrainingState state)
    {
        foreach (ITrainingStrategy strategy in _Strategies)
        {
            strategy.Reset(requireNewPreparation: strategy == _ActiveStrategy);
        }

        _ActiveStrategy = null;
        State = state;
    }

    private void ResetStrategies()
    {
        foreach (ITrainingStrategy strategy in _Strategies)
        {
            strategy.Reset();
        }
    }

    private void Announce(List<TrainingEvent> events, TrainingAnnouncementKind kind, string text, TrainingVariant? variant = null)
        => events.Add(new TrainingAnnouncementRequested(_Attempt!, ++_AnnouncementId, kind, text, variant));

    private bool IsStale(Frame frame) => _LastTimestampNs.HasValue && frame.TimestampNs <= _LastTimestampNs.Value;

    private TrainingFrameResult Result(LiveTrackingSnapshot snapshot, IReadOnlyList<TrainingEvent> events, bool tableUpdate, bool sceneUpdate)
        => new(snapshot, events, tableUpdate, sceneUpdate, State, _Attempt, _Variant);

    private static LiveTrackingSnapshot EmptySnapshot(Frame frame)
        => new(frame, LiveTrackingPhase.BallLost, false, default, default, [], BallPossession.None, BallPossession.None, 0, 0, null);
}
