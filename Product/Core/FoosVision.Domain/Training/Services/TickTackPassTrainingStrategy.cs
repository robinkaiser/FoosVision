// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public class TickTackPassTrainingStrategy : ITrainingStrategy
{
    private const double _BandSideFraction = 0.25;

    // A reversal must move this far from the previous extreme; subpixel jitter is not a tick-tack.
    public const double DirectionChangeMinDistancePx = 5;

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

    public TickTackPassTrainingStrategy(TrainingStrategyOptions options, TableConfiguration tableConfig)
    {
        Options = options;
        _TableConfig = tableConfig;
    }

    public TrainingStrategyOptions Options { get; }
    public string ReadyText => "Tick tack ready";
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

        return _Reversals >= 2
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

            bool backwards = TrainingGeometry.AttackProgress(_TableConfig, preparation.Team, snapshot.BallPosition, preparation.Bar) < 0;
            bool sameSide = GetSetupSide(snapshot, preparation.Team) == _Side;
            if (sameSide && !backwards)
            {
                _LastSetupAnchor = new(snapshot.Frame, snapshot.BallPosition, snapshot.Possession);
                return new(TrainingExecutionState.Holding);
            }

            _MovementFrame = snapshot.Frame;
            _InvalidExit = backwards || snapshot.Possession == new BallPossession(preparation.Team, PossessionArea.FiveBar);
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

            _InvalidExit |= TrainingGeometry.AttackProgress(_TableConfig, preparation.Team, snapshot.BallPosition, preparation.Bar) < 0;
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
        _Preparation = null;
        _LastSetupAnchor = null;
        _MovementFrame = null;
        _ConfirmationFrame = null;
        _InvalidExit = false;
    }

    private TrainingExecution Result(TrainingExecutionState state)
        => new(state, _MovementFrame, _ConfirmationFrame, _LastSetupAnchor);

    private int GetSetupSide(LiveTrackingSnapshot snapshot, Team team)
    {
        if (team == Team.None || snapshot.Possession != new BallPossession(team, PossessionArea.FiveBar))
        {
            return 0;
        }

        double side = TrainingGeometry.SideFraction(_TableConfig, TrainingGeometry.FiveBar(team), snapshot.BallPosition);

        return
            side >= 0 &&
            side <= _BandSideFraction ? -1 : side >= 1 - _BandSideFraction &&
            side <= 1 ? 1 : 0;
    }
}
