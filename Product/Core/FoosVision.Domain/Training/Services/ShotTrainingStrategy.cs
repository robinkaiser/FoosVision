// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public abstract class ShotTrainingStrategy : ITrainingStrategy
{
    public const int PreparationDelayMs = 1000;
    public const int DecisionWindowMs = 750;
    public const int CompletionTailMs = 250;
    public const double HighSpeedKmh = 4;
    protected const double StationaryMaxDistanceSquared = 50;

    private readonly List<(Frame Frame, Point Position)> _PreparationHistory = [];
    private TrainingPreparation? _Preparation;
    private TrainingAnchor? _LastSetupAnchor;
    private Frame? _MovementFrame;
    private Frame? _ConfirmationFrame;
    private bool _HasHighSpeed;
    private bool _HasTargetDetection;
    private bool _RequireNewPreparation;
    private bool _IsHolding;
    private Team _PreparingTeam = Team.None;
    private Point _ReferencePosition;
    private TableImageScale _Scale;

    protected ShotTrainingStrategy(TrainingStrategyOptions options, TableConfiguration tableConfig)
    {
        Options = options;
        TableConfig = tableConfig;
        _Scale = TableImageScale.From(tableConfig);
    }

    public TrainingStrategyOptions Options { get; }
    public abstract string ReadyText { get; }
    public abstract IReadOnlyList<TrainingVariant> Variants { get; }
    protected TableConfiguration TableConfig { get; private set; }
    protected abstract bool RequiresRest { get; }

    protected abstract bool IsInSetup(LiveTrackingSnapshot snapshot, Team team);

    public TrainingPreparation? FindPreparation(LiveTrackingSnapshot snapshot)
    {
        if (!snapshot.IsBallFound)
        {
            if (snapshot.Phase == LiveTrackingPhase.BallLost)
            {
                ClearPreparationHistory();
            }

            return null;
        }

        Team team = snapshot.Possession.Team;
        bool inSetup = team != Team.None && IsInSetup(snapshot, team);
        if (_RequireNewPreparation)
        {
            bool unchanged = inSetup && _Preparation?.Team == team &&
                (!RequiresRest || TrainingGeometry.DistanceSquared(snapshot.BallPosition, _ReferencePosition) <= StationaryMaxDistanceSquared);
            if (unchanged)
            {
                return null;
            }

            _RequireNewPreparation = false;
        }

        if (!inSetup)
        {
            ClearPreparationHistory();
            return null;
        }

        if (team != _PreparingTeam)
        {
            ClearPreparationHistory();
            _PreparingTeam = team;
        }

        _PreparationHistory.Add((snapshot.Frame, snapshot.BallPosition));
        long cutoff = snapshot.Frame.TimestampNs - (PreparationDelayMs * 1_000_000L);
        while (_PreparationHistory.Count > 1 && _PreparationHistory[1].Frame.TimestampNs <= cutoff)
        {
            _PreparationHistory.RemoveAt(0);
        }

        Point mean = new(_PreparationHistory.Average(item => item.Position.X), _PreparationHistory.Average(item => item.Position.Y));
        if (RequiresRest && _PreparationHistory.Any(item => TrainingGeometry.DistanceSquared(item.Position, mean) > StationaryMaxDistanceSquared))
        {
            ClearPreparationHistory();
            _PreparingTeam = team;
            _PreparationHistory.Add((snapshot.Frame, snapshot.BallPosition));
            return null;
        }

        if (_PreparationHistory[0].Frame.TimestampNs > cutoff)
        {
            return null;
        }

        _ReferencePosition = mean;
        return new(team, TrainingGeometry.ThreeBar(team), new(snapshot.Frame, snapshot.BallPosition, snapshot.Possession));
    }

    public void Begin(TrainingPreparation preparation)
    {
        _Preparation = preparation;
        _LastSetupAnchor = preparation.Anchor;
        _MovementFrame = null;
        _ConfirmationFrame = null;
        _HasHighSpeed = false;
        _HasTargetDetection = false;
        _IsHolding = true;
    }

    public TrainingExecution Evaluate(LiveTrackingSnapshot snapshot)
    {
        TrainingPreparation preparation = _Preparation ?? throw new InvalidOperationException("No active preparation.");
        if (snapshot.IsBallFound)
        {
            _IsHolding = IsInSetup(snapshot, preparation.Team) &&
                (!RequiresRest || TrainingGeometry.DistanceSquared(snapshot.BallPosition, _ReferencePosition) <= StationaryMaxDistanceSquared);
        }
        else if (snapshot.Phase == LiveTrackingPhase.BallLost)
        {
            _IsHolding = false;
        }

        if (!_MovementFrame.HasValue)
        {
            if (!snapshot.IsBallFound)
            {
                return new(snapshot.Phase == LiveTrackingPhase.BallLost ? TrainingExecutionState.Lost : TrainingExecutionState.Holding);
            }

            if (_IsHolding)
            {
                _HasHighSpeed = IsHighSpeed(snapshot);
                _LastSetupAnchor = new(snapshot.Frame, snapshot.BallPosition, snapshot.Possession);
                return new(TrainingExecutionState.Holding);
            }

            _MovementFrame = snapshot.Frame;
        }

        if (!_ConfirmationFrame.HasValue && snapshot.Frame.TimestampNs - _MovementFrame.Value.TimestampNs > DecisionWindowMs * 1_000_000L)
        {
            return Result(TrainingExecutionState.Lost);
        }

        if (snapshot.IsBallFound)
        {
            _HasHighSpeed |= IsHighSpeed(snapshot);
            _HasTargetDetection |= snapshot.ClosestBar == TrainingGeometry.TargetTwoBar(preparation.Team);
        }

        if (!_ConfirmationFrame.HasValue && _HasHighSpeed && _HasTargetDetection)
        {
            _ConfirmationFrame = snapshot.Frame;
        }

        if (_ConfirmationFrame.HasValue)
        {
            bool complete = snapshot.Frame.TimestampNs - _ConfirmationFrame.Value.TimestampNs >= CompletionTailMs * 1_000_000L;
            return Result(complete ? TrainingExecutionState.Completed : TrainingExecutionState.Pending);
        }

        bool expired = snapshot.Frame.TimestampNs - _MovementFrame.Value.TimestampNs >= DecisionWindowMs * 1_000_000L;
        return Result(expired ? TrainingExecutionState.Lost : TrainingExecutionState.Pending);
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        TableConfig = tableConfig;
        _Scale = TableImageScale.From(tableConfig);
        // Small calibration corrections do not reset the attempt or its history.
    }

    public void Reset(bool requireNewPreparation = false)
    {
        ClearPreparationHistory();
        _RequireNewPreparation = requireNewPreparation && _IsHolding;
        if (!requireNewPreparation)
        {
            _Preparation = null;
        }

        _MovementFrame = null;
        _ConfirmationFrame = null;
        _LastSetupAnchor = null;
        _HasHighSpeed = false;
        _HasTargetDetection = false;
    }

    private TrainingExecution Result(TrainingExecutionState state)
        => new(state, _MovementFrame, _ConfirmationFrame, _LastSetupAnchor);

    private bool IsHighSpeed(LiveTrackingSnapshot snapshot)
        => _Scale.ConvertGoalAxisSpeedPxPerSToKmh(Math.Abs(snapshot.BallVelocityPxPerS.X)) >= HighSpeedKmh ||
           _Scale.ConvertSideAxisSpeedPxPerSToKmh(Math.Abs(snapshot.BallVelocityPxPerS.Y)) >= HighSpeedKmh;

    private void ClearPreparationHistory()
    {
        _PreparationHistory.Clear();
        _PreparingTeam = Team.None;
    }
}
