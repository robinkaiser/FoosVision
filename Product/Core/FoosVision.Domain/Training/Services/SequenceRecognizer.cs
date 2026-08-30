// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public class SequenceRecognizer : ISequenceRecognizer
{
    private const int _StationaryBallDecidingTime_ns = 1000 * 1_000_000;
    private const int _StationaryBallDecidingMaxPositionDeltaSquared_Lower = (5 * 5) + (5 * 5); // dX² + dY²
    private const int _StationaryBallDecidingMaxPositionDeltaSquared_Upper = (15 * 15) + (15 * 15); // dX² + dY²

    private readonly Queue<TrackedBall> _TrackedBallsCache;

    private SequenceState _State;

    public SequenceRecognizer(TableConfiguration tableConfig)
    {
        TableConfig = tableConfig;
        _TrackedBallsCache = new();
    }

    public TableConfiguration TableConfig { get; set; }

    public void Reset()
    {
        _TrackedBallsCache.Clear();
        _State = SequenceState.Idle;
    }

    public SequenceState Process(TrackedBall trackedBall, BallPossession possession)
    {
        _TrackedBallsCache.Enqueue(trackedBall);
        var firstBall = _TrackedBallsCache.Peek();
        var overallCacheTimeNs = trackedBall.Frame.TimestampNs - firstBall.Frame.TimestampNs;

        var isInitalCacheFilling = overallCacheTimeNs < _StationaryBallDecidingTime_ns;
        if (isInitalCacheFilling) return SequenceState.Idle;

        if (overallCacheTimeNs > _StationaryBallDecidingTime_ns)
        {
            _ = _TrackedBallsCache.Dequeue();
        }

        switch (_State)
        {
            case SequenceState.Idle:
                if (IsPassSetupCompleted(possession))
                {
                    _State = SequenceState.PassSetupCompleted;
                }
                else if (IsShotSetupCompleted(possession))
                {
                    _State = SequenceState.ShotSetupCompleted;
                }
                break;

            case SequenceState.PassSetupCompleted:
                if (!IsStationaryBall(_StationaryBallDecidingMaxPositionDeltaSquared_Upper))
                {
                    _State = SequenceState.PassSequenceRunning;
                }
                break;

            case SequenceState.ShotSetupCompleted:
                if (!IsStationaryBall(_StationaryBallDecidingMaxPositionDeltaSquared_Upper))
                {
                    _State = SequenceState.ShotSequenceRunning;
                }
                break;

            case SequenceState.PassSequenceRunning:
                if (possession.Area != PossessionArea.FiveBar)
                {
                    _State = SequenceState.SequenceCompleted;
                }
                break;

            case SequenceState.ShotSequenceRunning:
                if (possession.Area != PossessionArea.ThreeBar)
                {
                    _State = SequenceState.SequenceCompleted;
                }
                break;

            case SequenceState.SequenceCompleted:
            default:
                break;
        }

        return _State;
    }

    private bool IsPassSetupCompleted(BallPossession possession)
    {
        var boundary = TableConfig.Field.Boundary;
        var upperBounds = (boundary.UpperLeft.Y + boundary.UpperRight.Y) / 2;
        var lowerBounds = (boundary.LowerLeft.Y + boundary.LowerRight.Y) / 2;

        var isTeamAPassPosition =
            possession.Team == Team.A &&
            possession.Area == PossessionArea.FiveBar;
        var isTeamBPassPosition =
            possession.Team == Team.B &&
            possession.Area == PossessionArea.FiveBar;

        var lastBall = _TrackedBallsCache.Last();
        var isBallInUpperThirdArea = lastBall.Position.Y < (((lowerBounds - upperBounds) * 0.33) + upperBounds);
        var isBallInLowerThirdArea = lastBall.Position.Y > (((lowerBounds - upperBounds) * 0.66) + upperBounds);

        var isPassPosition =
            (isTeamAPassPosition && isBallInLowerThirdArea) ||
            (isTeamBPassPosition && isBallInUpperThirdArea);

        if (!isPassPosition) return false;

        return IsStationaryBall(_StationaryBallDecidingMaxPositionDeltaSquared_Lower);
    }

    private bool IsShotSetupCompleted(BallPossession possession)
    {
        var boundary = TableConfig.Field.Boundary;
        var upperBounds = (boundary.UpperLeft.Y + boundary.UpperRight.Y) / 2;
        var lowerBounds = (boundary.LowerLeft.Y + boundary.LowerRight.Y) / 2;

        var isTeamAShotPosition =
            possession.Team == Team.A &&
            possession.Area == PossessionArea.ThreeBar;
        var isTeamBShotPosition =
            possession.Team == Team.B &&
            possession.Area == PossessionArea.ThreeBar;

        var lastBall = _TrackedBallsCache.Last();
        var isBallInUpperHalfArea = lastBall.Position.Y < (((lowerBounds - upperBounds) * 0.5) + upperBounds);
        var isBallInLowerHalfArea = lastBall.Position.Y > (((lowerBounds - upperBounds) * 0.5) + upperBounds);

        var isShotPosition =
            (isTeamAShotPosition && isBallInUpperHalfArea) ||
            (isTeamBShotPosition && isBallInLowerHalfArea);

        if (!isShotPosition) return false;

        return IsStationaryBall(_StationaryBallDecidingMaxPositionDeltaSquared_Lower);
    }

    private bool IsStationaryBall(int maxSquaredDistance)
    {
        int count = _TrackedBallsCache.Count;
        double sumX = 0;
        double sumY = 0;

        foreach (var ball in _TrackedBallsCache)
        {
            var p = ball.Position;
            sumX += p.X;
            sumY += p.Y;
        }

        double avgX = sumX / count;
        double avgY = sumY / count;

        foreach (var ball in _TrackedBallsCache)
        {
            var p = ball.Position;
            double deltaSquared = ((avgX - p.X) * (avgX - p.X)) + ((avgY - p.Y) * (avgY - p.Y));

            if (deltaSquared > maxSquaredDistance)
            {
                return false;
            }
        }

        return true;
    }
}
