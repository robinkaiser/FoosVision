// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.Services;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.ReplayDecision.Strategies;

// State machine:
//
//   WaitingForObservation
//     |
//     | observed frame
//     v
//   BallObserved
//     |\
//     | \ further observed frames:
//     |  \ update history and last observed frame at time t1
//     |
//     | no observed ball
//     v
//   BallLostPending
//     |
//     | no observed ball until t1 + 1s
//     v
//   BallLostEvaluated
//     |
//     | choose newest contiguous candidate run [s0..s1] with the same BarType and duration >= 1s
//     | anchor time ta is latest candidate at or before s1 - 400ms
//     | require
//     |   one high-speed hit between ta and t1, or
//     |   all candidates between ta and t1 to be in the middle field third in front of the goal
//     v
//   Replay accepted, otherwise wait for observation again
//
public class BallDisappearedReplayStrategy : IReplayDecisionStrategy
{
    private enum DecisionState
    {
        WaitingForObservation,
        BallObserved,
        BallLostEvaluated,
    }

    private readonly record struct AnchorCandidate(
        Frame Frame,
        Point Position,
        BallPossession Possession,
        int PossessionTimeMs,
        BarType Bar,
        Vector2 VelocityPxPerS);

    private readonly record struct ThreeBarFrontThird(
        double StartY,
        double EndY);

    private readonly record struct TriggerRequirements(
        bool HasHighSpeed,
        bool AreAllCandidatesInFrontOfGoal);

    private const long _HistoryDurationNs = 5_000L * 1_000_000L;
    private const long _MinimumBarPossessionDurationNs = 1_000L * 1_000_000L;
    private const long _AnchorBeforeBarExitNs = 400L * 1_000_000L;
    private const long _ReplayDecisionDelayNs = 1_000L * 1_000_000L;

    private const double _HighSideSpeedThresholdKmh = 4.0;
    private const double _HighGoalSpeedThresholdKmh = 6.0;

    private readonly List<AnchorCandidate> _Candidates = [];
    private TableImageScale _TableImageScale;
    private ThreeBarFrontThird _AThreeBarFrontThird;
    private ThreeBarFrontThird _BThreeBarFrontThird;
    private Frame? _LastObservedFrame;
    private DecisionState _State;

    public BallDisappearedReplayStrategy(TableConfiguration tableConfiguration)
    {
        _TableImageScale = TableImageScale.From(tableConfiguration);
        _AThreeBarFrontThird = CreateThreeBarFrontThird(tableConfiguration, BarType.A3);
        _BThreeBarFrontThird = CreateThreeBarFrontThird(tableConfiguration, BarType.B3);
    }

    public Option<ReplayAnchor> Decide(Frame frame, bool isBallObserved, ReplayCandidate? candidate)
    {
        Prune(frame.TimestampNs);

        if (isBallObserved)
        {
            _LastObservedFrame = frame;
            SetDecisionState(DecisionState.BallObserved);

            if (candidate.HasValue)
            {
                AddCandidate(candidate.Value);
            }

            return Option<ReplayAnchor>.None();
        }

        if (_State != DecisionState.BallObserved ||
            !_LastObservedFrame.HasValue)
        {
            return Option<ReplayAnchor>.None();
        }

        long elapsedSinceLastObservedNs = frame.TimestampNs - _LastObservedFrame.Value.TimestampNs;

        if (elapsedSinceLastObservedNs < _ReplayDecisionDelayNs)
        {
            return Option<ReplayAnchor>.None();
        }

        SetDecisionState(DecisionState.BallLostEvaluated);

        var anchor = TryCreateAnchor(frame.TimestampNs, _LastObservedFrame.Value.TimestampNs);
        _Candidates.Clear();

        return anchor;
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        _TableImageScale = TableImageScale.From(tableConfig);
        _AThreeBarFrontThird = CreateThreeBarFrontThird(tableConfig, BarType.A3);
        _BThreeBarFrontThird = CreateThreeBarFrontThird(tableConfig, BarType.B3);
    }

    public void Reset()
    {
        _Candidates.Clear();
        _LastObservedFrame = null;
        SetDecisionState(DecisionState.WaitingForObservation);
    }

    private void AddCandidate(ReplayCandidate candidate)
    {
        AnchorCandidate anchorCandidate = new(
            candidate.Frame,
            candidate.Position,
            candidate.Possession,
            candidate.PossessionTimeMs,
            candidate.Bar,
            candidate.VelocityPxPerS);
        _Candidates.Add(anchorCandidate);

        Prune(anchorCandidate.Frame.TimestampNs);
    }

    private Option<ReplayAnchor> TryCreateAnchor(long timestampNs, long lastObservedTimestampNs)
    {
        Prune(timestampNs);

        if (_Candidates.Count == 0)
        {
            return Option<ReplayAnchor>.None();
        }

        for (int endIndex = _Candidates.Count - 1; endIndex >= 0;)
        {
            int startIndex = FindSegmentStart(_Candidates, endIndex);

            AnchorCandidate first = _Candidates[startIndex];
            AnchorCandidate last = _Candidates[endIndex];
            long segmentDurationNs = last.Frame.TimestampNs - first.Frame.TimestampNs;

            if (segmentDurationNs >= _MinimumBarPossessionDurationNs)
            {
                var anchor = CreateAnchor(_Candidates, startIndex, endIndex);
                TriggerRequirements triggerRequirements = EvaluateTriggerRequirements(
                    anchor.Frame.TimestampNs,
                    lastObservedTimestampNs,
                    last.Bar);

                if (!triggerRequirements.HasHighSpeed &&
                    !triggerRequirements.AreAllCandidatesInFrontOfGoal)
                {
                    return Option<ReplayAnchor>.None();
                }

                return Option<ReplayAnchor>.Some(anchor);
            }

            endIndex = startIndex - 1;
        }

        return Option<ReplayAnchor>.None();
    }

    private void Prune(long timestampNs)
    {
        long cutoffTimestampNs = timestampNs - _HistoryDurationNs;

        int removeCount = 0;

        while (removeCount < _Candidates.Count &&
               _Candidates[removeCount].Frame.TimestampNs < cutoffTimestampNs)
        {
            removeCount++;
        }

        if (removeCount != 0)
        {
            _Candidates.RemoveRange(0, removeCount);
        }
    }

    private static int FindSegmentStart(List<AnchorCandidate> candidates, int endIndex)
    {
        int startIndex = endIndex;
        var bar = candidates[endIndex].Bar;

        while (startIndex > 0 &&
               candidates[startIndex - 1].Bar == bar)
        {
            startIndex--;
        }

        return startIndex;
    }

    private static ReplayAnchor CreateAnchor(List<AnchorCandidate> candidates, int startIndex, int endIndex)
    {
        long targetTimestampNs = candidates[endIndex].Frame.TimestampNs - _AnchorBeforeBarExitNs;

        for (int i = endIndex; i >= startIndex; i--)
        {
            AnchorCandidate candidate = candidates[i];

            if (candidate.Frame.TimestampNs <= targetTimestampNs)
            {
                return new ReplayAnchor(
                    candidate.Frame,
                    candidate.Position,
                    candidate.Possession,
                    candidate.PossessionTimeMs,
                    ReplayTriggerKind.BallDisappeared);
            }
        }

        AnchorCandidate first = candidates[startIndex];

        return new ReplayAnchor(
            first.Frame,
            first.Position,
            first.Possession,
            first.PossessionTimeMs,
            ReplayTriggerKind.BallDisappeared);
    }

    private TriggerRequirements EvaluateTriggerRequirements(
        long startTimestampNs,
        long endTimestampNs,
        BarType anchorBar)
    {
        bool hasCandidateInWindow = false;
        bool hasHighSpeed = false;
        bool areAllCandidatesInFrontOfGoal = true;
        ThreeBarFrontThird threeBarFrontThird = GetThreeBarFrontThird(anchorBar);

        foreach (AnchorCandidate candidate in _Candidates)
        {
            bool isInWindow =
                candidate.Frame.TimestampNs >= startTimestampNs &&
                candidate.Frame.TimestampNs <= endTimestampNs;

            if (!isInWindow)
            {
                continue;
            }

            hasCandidateInWindow = true;
            (double goalSpeedKmh, double sideSpeedKmh) = GetSpeedsKmh(candidate);
            bool isHighSpeed =
                goalSpeedKmh > _HighGoalSpeedThresholdKmh ||
                sideSpeedKmh > _HighSideSpeedThresholdKmh;
            bool isInFrontOfGoal = IsInFrontOfGoal(candidate, threeBarFrontThird);

            if (isHighSpeed)
            {
                hasHighSpeed = true;
            }

            if (!isInFrontOfGoal)
            {
                areAllCandidatesInFrontOfGoal = false;
            }
        }

        return new TriggerRequirements(
            hasHighSpeed,
            hasCandidateInWindow && areAllCandidatesInFrontOfGoal);
    }

    private (double GoalSpeedKmh, double SideSpeedKmh) GetSpeedsKmh(AnchorCandidate candidate)
        => (
            _TableImageScale.ConvertGoalAxisSpeedPxPerSToKmh(Math.Abs(candidate.VelocityPxPerS.X)),
            _TableImageScale.ConvertSideAxisSpeedPxPerSToKmh(Math.Abs(candidate.VelocityPxPerS.Y)));

    private ThreeBarFrontThird GetThreeBarFrontThird(BarType anchorBar)
    {
        Team anchorTeam = TableBarClassifier.GetTeam(anchorBar);

        return anchorTeam == Team.A ? _AThreeBarFrontThird : _BThreeBarFrontThird;
    }

    private bool IsInFrontOfGoal(AnchorCandidate candidate, ThreeBarFrontThird threeBarFrontThird)
        => candidate.Position.Y >= threeBarFrontThird.StartY &&
           candidate.Position.Y <= threeBarFrontThird.EndY;

    private static ThreeBarFrontThird CreateThreeBarFrontThird(TableConfiguration tableConfiguration, BarType threeBar)
    {
        Bar bar = tableConfiguration.Field.Bars[threeBar];
        double x = (bar.Center.P0.X + bar.Center.P1.X) / 2.0;
        Trapezium boundary = tableConfiguration.Field.Boundary;
        double upperY = InterpolateY(boundary.UpperLeft, boundary.UpperRight, x);
        double lowerY = InterpolateY(boundary.LowerLeft, boundary.LowerRight, x);
        double minY = Math.Min(upperY, lowerY);
        double maxY = Math.Max(upperY, lowerY);
        double thirdHeight = (maxY - minY) / 3.0;

        return new ThreeBarFrontThird(
            minY + thirdHeight,
            minY + (2.0 * thirdHeight));
    }

    private static double InterpolateY(Point a, Point b, double x)
    {
        if (a.X == b.X)
        {
            return (a.Y + b.Y) / 2.0;
        }

        double t = (x - a.X) / (b.X - a.X);

        return a.Y + (t * (b.Y - a.Y));
    }

    private void SetDecisionState(DecisionState state)
    {
        if (_State == state)
        {
            return;
        }

        _State = state;
    }
}
