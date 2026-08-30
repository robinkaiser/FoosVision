// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.ReplayDecision.Strategies;

// State machine:
//
//   Idle
//     |
//     | ThreeBar possession lasts >= 3s, qualified at time t0
//     v
//   ArmedThreeBarPossession
//     |\
//     | \ candidate is missing:
//     |  \ keep armed; this is not a ThreeBar exit
//     |
//     | candidate possession leaves ThreeBar at time t1
//     v
//   PendingShot
//     |
//     | anchor time ta is newest ThreeBar candidate timestamp <= t1 - 400ms
//     | require high speed between t0 and t1 + 500ms
//     | require observed ball on Defense or ThreeBar between t1 + 1.0s and t1 + 1.5s
//     v
//   Replay accepted, otherwise back to Idle
//
public class SavedShotReplayStrategy : IReplayDecisionStrategy
{
    private enum DecisionState
    {
        Idle,
        ArmedThreeBarPossession,
        PendingShot,
    }

    private readonly record struct PendingShot(
        long ThreeBarExitTimestampNs,
        ReplayAnchor Anchor,
        bool HasHighSpeed);

    private const long _MinimumThreeBarPossessionDurationNs = 3_000L * 1_000_000L;
    private const long _HighSpeedWindowAfterExitNs = 500L * 1_000_000L;
    private const long _ReplayDecisionWindowStartAfterExitNs = 1_000L * 1_000_000L;
    private const long _ReplayDecisionWindowEndAfterExitNs = 1_500L * 1_000_000L;
    private const long _AnchorBeforeBarExitNs = 400L * 1_000_000L;

    private const double _HighSideSpeedThresholdKmh = 4.0;
    private const double _HighGoalSpeedThresholdKmh = 6.0;

    private readonly List<ReplayCandidate> _ThreeBarCandidates = [];

    private TableImageScale _TableImageScale;
    private PendingShot? _PendingShot;
    private bool _ThreeBarPossessionQualified;
    private long? _ThreeBarQualifiedTimestampNs;
    private bool _HasHighSpeedSinceThreeBarQualified;
    private DecisionState _State;

    public SavedShotReplayStrategy(TableImageScale tableImageScale)
    {
        _TableImageScale = tableImageScale;
    }

    public Option<ReplayAnchor> Decide(Frame frame, bool isBallObserved, ReplayCandidate? candidate)
    {
        if (_PendingShot.HasValue)
        {
            Option<ReplayAnchor> anchor = AdvancePendingShot(frame, isBallObserved, candidate);

            if (anchor.IsSome)
            {
                return anchor;
            }

            return Option<ReplayAnchor>.None();
        }

        AdvanceThreeBarPossession(frame, candidate);

        if (!_PendingShot.HasValue)
        {
            return Option<ReplayAnchor>.None();
        }

        return AdvancePendingShot(frame, isBallObserved, candidate);
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        _TableImageScale = TableImageScale.From(tableConfig);
    }

    public void Reset()
        => ResetAll();

    private void ResetAll()
    {
        _PendingShot = null;
        ResetThreeBarCandidates();
        SetDecisionState(DecisionState.Idle);
    }

    private Option<ReplayAnchor> AdvancePendingShot(Frame frame, bool isBallObserved, ReplayCandidate? candidate)
    {
        PendingShot pendingShot = _PendingShot!.Value;
        long elapsedSinceExitNs = frame.TimestampNs - pendingShot.ThreeBarExitTimestampNs;

        if (candidate.HasValue &&
            elapsedSinceExitNs <= _HighSpeedWindowAfterExitNs &&
            IsHighSpeed(candidate.Value))
        {
            pendingShot = pendingShot with { HasHighSpeed = true };
            _PendingShot = pendingShot;
        }

        if (elapsedSinceExitNs < _ReplayDecisionWindowStartAfterExitNs)
        {
            return Option<ReplayAnchor>.None();
        }

        if (pendingShot.HasHighSpeed &&
            isBallObserved &&
            candidate.HasValue &&
            IsReplayTargetPossession(candidate.Value.Possession))
        {
            ResetAll();
            return Option<ReplayAnchor>.Some(pendingShot.Anchor);
        }

        if (elapsedSinceExitNs <= _ReplayDecisionWindowEndAfterExitNs)
        {
            return Option<ReplayAnchor>.None();
        }

        _PendingShot = null;
        SetDecisionState(DecisionState.Idle);

        return Option<ReplayAnchor>.None();
    }

    private void AdvanceThreeBarPossession(Frame frame, ReplayCandidate? candidate)
    {
        if (candidate.HasValue &&
            IsThreeBarPossession(candidate.Value))
        {
            RecordThreeBarCandidate(candidate.Value);
            return;
        }

        if (_ThreeBarCandidates.Count == 0)
        {
            return;
        }

        if (!candidate.HasValue)
        {
            if (!HasQualifiedThreeBarPossession())
            {
                ResetThreeBarCandidates();
            }

            return;
        }

        if (HasQualifiedThreeBarPossession())
        {
            StartPendingShot(frame, candidate);
            return;
        }

        ResetThreeBarCandidates();
    }

    private void RecordThreeBarCandidate(ReplayCandidate candidate)
    {
        if (_ThreeBarCandidates.Count != 0 &&
            _ThreeBarCandidates[^1].Bar != candidate.Bar)
        {
            ResetThreeBarCandidates();
        }

        _ThreeBarCandidates.Add(candidate);

        if (!HasQualifiedThreeBarPossession())
        {
            return;
        }

        if (!_ThreeBarPossessionQualified)
        {
            _ThreeBarPossessionQualified = true;
            _ThreeBarQualifiedTimestampNs = candidate.Frame.TimestampNs;
            SetDecisionState(DecisionState.ArmedThreeBarPossession);
        }

        RecordHighSpeedAfterThreeBarQualified(candidate);
    }

    private bool HasQualifiedThreeBarPossession()
    {
        ReplayCandidate first = _ThreeBarCandidates[0];
        ReplayCandidate last = _ThreeBarCandidates[^1];

        long durationNs = last.Frame.TimestampNs - first.Frame.TimestampNs;
        bool isQualified = durationNs >= _MinimumThreeBarPossessionDurationNs;

        return isQualified;
    }

    private void StartPendingShot(Frame frame, ReplayCandidate? candidate)
    {
        long exitTimestampNs = frame.TimestampNs;
        bool hasHighSpeed = _HasHighSpeedSinceThreeBarQualified;

        if (candidate.HasValue &&
            IsHighSpeed(candidate.Value))
        {
            hasHighSpeed = true;
        }

        ReplayAnchor anchor = CreateAnchor(exitTimestampNs);
        _PendingShot = new(
            exitTimestampNs,
            anchor,
            hasHighSpeed);

        ResetThreeBarCandidates();
        SetDecisionState(DecisionState.PendingShot);
    }

    private void RecordHighSpeedAfterThreeBarQualified(ReplayCandidate candidate)
    {
        if (_HasHighSpeedSinceThreeBarQualified ||
            !_ThreeBarQualifiedTimestampNs.HasValue ||
            candidate.Frame.TimestampNs < _ThreeBarQualifiedTimestampNs.Value ||
            !IsHighSpeed(candidate))
        {
            return;
        }

        _HasHighSpeedSinceThreeBarQualified = true;
    }

    private ReplayAnchor CreateAnchor(long exitTimestampNs)
    {
        long targetTimestampNs = exitTimestampNs - _AnchorBeforeBarExitNs;

        for (int i = _ThreeBarCandidates.Count - 1; i >= 0; i--)
        {
            ReplayCandidate candidate = _ThreeBarCandidates[i];

            if (candidate.Frame.TimestampNs <= targetTimestampNs)
            {
                return CreateAnchor(candidate);
            }
        }

        return CreateAnchor(_ThreeBarCandidates[0]);
    }

    private static ReplayAnchor CreateAnchor(ReplayCandidate candidate)
        => new(
            candidate.Frame,
            candidate.Position,
            candidate.Possession,
            candidate.PossessionTimeMs,
            ReplayTriggerKind.SavedShot);

    private bool IsHighSpeed(ReplayCandidate candidate)
    {
        (double goalSpeedKmh, double sideSpeedKmh) = GetSpeedsKmh(candidate);

        return
            goalSpeedKmh > _HighGoalSpeedThresholdKmh ||
            sideSpeedKmh > _HighSideSpeedThresholdKmh;
    }

    private (double GoalSpeedKmh, double SideSpeedKmh) GetSpeedsKmh(ReplayCandidate candidate)
        => (
            _TableImageScale.ConvertGoalAxisSpeedPxPerSToKmh(Math.Abs(candidate.VelocityPxPerS.X)),
            _TableImageScale.ConvertSideAxisSpeedPxPerSToKmh(Math.Abs(candidate.VelocityPxPerS.Y)));

    private static bool IsThreeBarPossession(ReplayCandidate candidate)
        => candidate.Possession.Area == PossessionArea.ThreeBar;

    private static bool IsReplayTargetPossession(BallPossession possession)
        => possession.Area is PossessionArea.Defense or PossessionArea.ThreeBar;

    private void ResetThreeBarCandidates()
    {
        _ThreeBarCandidates.Clear();
        _ThreeBarPossessionQualified = false;
        _ThreeBarQualifiedTimestampNs = null;
        _HasHighSpeedSinceThreeBarQualified = false;

        if (!_PendingShot.HasValue)
        {
            SetDecisionState(DecisionState.Idle);
        }
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
