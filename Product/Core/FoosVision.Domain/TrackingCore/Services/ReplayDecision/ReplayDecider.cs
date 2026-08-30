// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.ReplayDecision;

public class ReplayDecider : IReplayDecider
{
    private readonly IReadOnlyList<IReplayDecisionStrategy> _Strategies;

    public ReplayDecider(IEnumerable<IReplayDecisionStrategy> strategies)
    {
        _Strategies = [.. strategies];
    }

    public Option<ReplayAnchor> Decide(Frame frame, bool isBallObserved, ReplayCandidate? candidate)
    {
        Option<ReplayAnchor> selectedAnchor = Option<ReplayAnchor>.None();

        foreach (IReplayDecisionStrategy strategy in _Strategies)
        {
            Option<ReplayAnchor> anchor = strategy.Decide(frame, isBallObserved, candidate);

            if (selectedAnchor.IsNone &&
                anchor.IsSome)
            {
                selectedAnchor = anchor;
            }
        }

        if (selectedAnchor.IsSome)
        {
            ResetStrategies();
        }

        return selectedAnchor;
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        foreach (IReplayDecisionStrategy strategy in _Strategies)
        {
            strategy.UpdateTableConfig(tableConfig);
        }
    }

    private void ResetStrategies()
    {
        foreach (IReplayDecisionStrategy strategy in _Strategies)
        {
            strategy.Reset();
        }
    }
}
