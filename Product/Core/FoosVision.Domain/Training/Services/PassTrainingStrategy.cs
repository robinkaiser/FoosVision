// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public class PassTrainingStrategy : ITrainingStrategy
{
    private const double _EntrySideFraction = 0.20;
    private const double _HoldingSideFraction = 0.50;

    public const double DirectionChangeMinDistancePx = 20;

    private readonly List<(Frame Frame, Point Position)> _RestHistory = [];
    private TableConfiguration _TableConfig;
    private Team _Team = Team.None;
    private int _Side;
    private int _Direction;
    private int _Reversals;
    private double _ExtremeY;
    private TrainingPreparation? _Preparation;
    private TrainingAnchor? _LastSetupAnchor;
    private Frame? _MovementFrame;
    private Frame? _ConfirmationFrame;
    private bool _InvalidExit;

    public PassTrainingStrategy(TrainingStrategyOptions options, TableConfiguration tableConfig)
    {
        Options = options;
        _TableConfig = tableConfig;
    }

    public TrainingStrategyOptions Options { get; }
    public string ReadyText => "Pass ready";
    public IReadOnlyList<TrainingVariant> Variants => Options.Variants;

    public TrainingPreparation? FindPreparation(LiveTrackingSnapshot snapshot)
    {
        if (!snapshot.IsBallFound)
        {
            if (snapshot.Phase == LiveTrackingPhase.BallLost)
            {
                Reset();
            }

            return null;
        }

        Team team = snapshot.Possession.Team;
        int side = GetSetupSide(snapshot, team);
        if (side == 0)
        {
            Reset();
            return null;
        }

        double y = snapshot.BallPosition.Y;
        if (_Team != team || _Side != side)
        {
            Reset();
            _Team = team;
            _Side = side;
            _ExtremeY = y;
            return null;
        }

        double delta = y - _ExtremeY;
        if (_Direction == 0)
        {
            if (Math.Abs(delta) >= DirectionChangeMinDistancePx)
            {
                _Direction = Math.Sign(delta);
                _ExtremeY = y;
            }

            return null;
        }

        if (Math.Sign(delta) == _Direction)
        {
            _ExtremeY = y;
        }
        else if (Math.Abs(delta) >= DirectionChangeMinDistancePx)
        {
            _Direction = -_Direction;
            _ExtremeY = y;
            _Reversals++;
        }

        return _Reversals >= 2 || (_Reversals == 1 && HasComeToRest(snapshot))
            ? new(team, TrainingGeometry.FiveBar(team), new(snapshot.Frame, snapshot.BallPosition, snapshot.Possession))
            : null;
    }

    public void Begin(TrainingPreparation preparation)
    {
        _Preparation = preparation;
        _LastSetupAnchor = preparation.Anchor;
    }

    public TrainingExecution Evaluate(LiveTrackingSnapshot snapshot)
    {
        TrainingPreparation preparation = _Preparation ?? throw new InvalidOperationException("No active preparation.");
        if (!_MovementFrame.HasValue)
        {
            if (!snapshot.IsBallFound)
            {
                return new(snapshot.Phase == LiveTrackingPhase.BallLost ? TrainingExecutionState.Lost : TrainingExecutionState.Holding);
            }

            bool sameSide = GetSetupSide(snapshot, preparation.Team) == _Side;
            if (sameSide)
            {
                _LastSetupAnchor = new(snapshot.Frame, snapshot.BallPosition, snapshot.Possession);
                return new(TrainingExecutionState.Holding);
            }

            _MovementFrame = snapshot.Frame;
            bool ownFive = snapshot.Possession == new BallPossession(preparation.Team, PossessionArea.FiveBar);
            bool backwards = !ownFive && HasMovedBackwards(snapshot.BallPosition, preparation);
            _InvalidExit = backwards || ownFive;
        }

        if (!_ConfirmationFrame.HasValue && snapshot.Frame.TimestampNs - _MovementFrame.Value.TimestampNs > ShotTrainingStrategy.DecisionWindowMs * 1_000_000L)
        {
            return Result(TrainingExecutionState.Lost);
        }

        if (!_InvalidExit && snapshot.IsBallFound)
        {
            bool ownThree = snapshot.Possession == new BallPossession(preparation.Team, PossessionArea.ThreeBar);
            bool opponentDefense = snapshot.Possession == new BallPossession(TrainingGeometry.Opponent(preparation.Team), PossessionArea.Defense);
            if (ownThree || opponentDefense)
            {
                _ConfirmationFrame ??= snapshot.Frame;
            }

            _InvalidExit |= HasMovedBackwards(snapshot.BallPosition, preparation);
        }

        if (!_InvalidExit && _ConfirmationFrame.HasValue)
        {
            return Result(TrainingExecutionState.Completed);
        }

        bool expired = snapshot.Frame.TimestampNs - _MovementFrame.Value.TimestampNs >= ShotTrainingStrategy.DecisionWindowMs * 1_000_000L;
        return Result(expired ? TrainingExecutionState.Lost : TrainingExecutionState.Pending);
    }

    public void UpdateTableConfig(TableConfiguration tableConfig) => _TableConfig = tableConfig;

    public void Reset(bool requireNewPreparation = false)
    {
        _Team = Team.None;
        _Side = 0;
        _Direction = 0;
        _Reversals = 0;
        _RestHistory.Clear();
        _Preparation = null;
        _LastSetupAnchor = null;
        _MovementFrame = null;
        _ConfirmationFrame = null;
        _InvalidExit = false;
    }

    private TrainingExecution Result(TrainingExecutionState state)
        => new(state, _MovementFrame, _ConfirmationFrame, _LastSetupAnchor);

    private bool HasComeToRest(LiveTrackingSnapshot snapshot)
    {
        _RestHistory.Add((snapshot.Frame, snapshot.BallPosition));
        long cutoff = snapshot.Frame.TimestampNs - ShotTrainingStrategy.PreparationDelayMs * 1_000_000L;
        while (_RestHistory.Count > 1 && _RestHistory[1].Frame.TimestampNs <= cutoff)
        {
            _RestHistory.RemoveAt(0);
        }

        Point mean = new(_RestHistory.Average(item => item.Position.X), _RestHistory.Average(item => item.Position.Y));
        if (_RestHistory.Any(item => TrainingGeometry.DistanceSquared(item.Position, mean) > ShotTrainingStrategy.StationaryMaxDistanceSquared))
        {
            _RestHistory.Clear();
            _RestHistory.Add((snapshot.Frame, snapshot.BallPosition));
            return false;
        }

        return _RestHistory[0].Frame.TimestampNs <= cutoff;
    }

    private bool HasMovedBackwards(Point position, TrainingPreparation preparation)
        => TrainingGeometry.AttackProgress(_TableConfig, preparation.Team, position, preparation.Bar) <
           TrainingGeometry.AttackProgress(_TableConfig, preparation.Team, preparation.Anchor.Position, preparation.Bar);

    private int GetSetupSide(LiveTrackingSnapshot snapshot, Team team)
    {
        if (team == Team.None || snapshot.Possession != new BallPossession(team, PossessionArea.FiveBar))
        {
            return 0;
        }

        double side = TrainingGeometry.SideFraction(_TableConfig, TrainingGeometry.FiveBar(team), snapshot.BallPosition);
        if (team == _Team &&
            ((_Side == -1 && side >= 0 && side <= _HoldingSideFraction) ||
             (_Side == 1 && side >= 1 - _HoldingSideFraction && side <= 1)))
        {
            return _Side;
        }

        return
            side >= 0 &&
            side <= _EntrySideFraction ? -1 : side >= 1 - _EntrySideFraction &&
            side <= 1 ? 1 : 0;
    }
}
