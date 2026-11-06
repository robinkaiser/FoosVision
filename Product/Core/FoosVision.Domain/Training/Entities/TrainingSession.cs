// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.CalibrationCore.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.Events;
using FoosVision.Domain.Training.Services;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Entities;

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
    private long? _ExecutionTimestampNs;
    private TrainingAttempt? _ReplayAttempt;
    private Frame? _ReplayTriggerFrame;
    private TrainingReturnToLiveRequested? _ReturnToLive;

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

            LiveTrackingSnapshot snapshot = _LiveTracker.ApplyObservations(frame, observations, includePredicted: true);
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

            if (_ReturnToLive is not null)
            {
                events.Add(_ReturnToLive);
            }

            bool tableUpdate = _TableUpdates.RequestTableUpdate(snapshot.Frame);
            bool sceneUpdate = _TableUpdates.RequestTableSceneUpdate(snapshot.Frame, snapshot.IsBallFound, snapshot.BallPosition, snapshot.BallCandidates);
            return Result(snapshot, events.AsReadOnly(), tableUpdate, sceneUpdate);
        }
    }

    public void Stop()
    {
        lock (_Gate)
        {
            if (State == TrainingState.Stopped)
            {
                return;
            }

            State = TrainingState.Stopped;
            ResetStrategies();
            _ActiveStrategy = null;
            _TableUpdates.CompleteTableUpdate();
            _TableUpdates.CompleteTableSceneUpdate();
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

        if (_ReplayAttempt is not null && _ReplayTriggerFrame is Frame triggerFrame)
        {
            _ReturnToLive = new(_ReplayAttempt, triggerFrame);
            _ReplayAttempt = null;
            _ReplayTriggerFrame = null;
        }

        _Attempt = new(Id, ++_AttemptId, selected.Options.Kind, preparation.Team, preparation.Bar, preparation.Anchor);
        _ActiveStrategy = selected;
        selected.Begin(preparation);
        _Variant = null;
        _ExecutionTimestampNs = null;
        _ReadyTimestampNs = snapshot.Frame.TimestampNs;
        _WaitNs = (long)selected.Options.Wait.Select(_Random.NextUnit()).TotalNanoseconds;
        State = TrainingState.Waiting;
        events.Add(new TrainingPreparationConfirmed(_Attempt));
        if (Options.VariantAnnouncement == TrainingVariantAnnouncement.OnReady)
        {
            _Variant = SelectVariant(selected);
            Announce(events, TrainingAnnouncementKind.Ready, $"{_Variant.Text} ready", _Variant);
            return;
        }

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

        if (_ExecutionTimestampNs.HasValue && execution.MovementFrame.HasValue &&
            execution.MovementFrame.Value.TimestampNs - _ExecutionTimestampNs.Value > ExecutionTimeoutMs * 1_000_000L)
        {
            Abort(events, TrainingAbortReason.ExecutionTimeout);
            return;
        }

        if (execution.State == TrainingExecutionState.Completed)
        {
            if (!_ExecutionTimestampNs.HasValue)
            {
                Abort(events, TrainingAbortReason.PrematureExecution);
                return;
            }

            events.Add(new TrainingAttemptCompleted(_Attempt!, _Variant!, execution.ConfirmationFrame!.Value, snapshot.Frame));
            _ReplayAttempt = _Attempt;
            _ReplayTriggerFrame = snapshot.Frame;
            _ReturnToLive = null;
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

        if (_ExecutionTimestampNs.HasValue && snapshot.Frame.TimestampNs - _ExecutionTimestampNs.Value >= ExecutionTimeoutMs * 1_000_000L)
        {
            Abort(events, TrainingAbortReason.ExecutionTimeout);
            return;
        }

        // A tracked ball is sufficient, including a prediction without a new observation.
        if (State == TrainingState.Waiting && snapshot.IsBallFound && snapshot.Frame.TimestampNs - _ReadyTimestampNs >= _WaitNs)
        {
            _ExecutionTimestampNs = snapshot.Frame.TimestampNs;
            State = TrainingState.AwaitingExecution;
            if (Options.VariantAnnouncement == TrainingVariantAnnouncement.OnReady)
            {
                Announce(events, TrainingAnnouncementKind.Shoot, "Shoot!", _Variant);
                return;
            }

            _Variant = SelectVariant(strategy);
            Announce(events, TrainingAnnouncementKind.Variant, _Variant.Text, _Variant);
        }
    }

    private TrainingVariant SelectVariant(ITrainingStrategy strategy)
    {
        double unit = _Random.NextUnit();
        TrainingWaitOptions.ValidateUnit(unit);
        double selection = unit * strategy.Variants.Sum(variant => variant.Weight);
        double cumulativeWeight = 0;
        foreach (TrainingVariant variant in strategy.Variants)
        {
            cumulativeWeight += variant.Weight;
            if (selection < cumulativeWeight)
            {
                return variant;
            }
        }

        // Multiplication can round up to the total weight even when unit is below 1.
        return strategy.Variants.Last(variant => variant.Weight > 0);
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
