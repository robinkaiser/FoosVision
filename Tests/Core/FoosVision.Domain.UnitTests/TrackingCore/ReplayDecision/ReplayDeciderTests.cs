// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.ReplayDecision;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.UnitTests.TrackingCore.ReplayDecision;

public class ReplayDeciderTests
{
    [Fact]
    public void Decide_returns_first_strategy_anchor_and_still_updates_all_strategies()
    {
        Frame frame = new(1, 1_000_000_000);
        ReplayAnchor firstAnchor = CreateAnchor(10);
        ReplayAnchor secondAnchor = CreateAnchor(20);
        RecordingReplayDecisionStrategy firstStrategy = new(Option<ReplayAnchor>.Some(firstAnchor));
        RecordingReplayDecisionStrategy secondStrategy = new(Option<ReplayAnchor>.Some(secondAnchor));
        ReplayDecider testee = new([firstStrategy, secondStrategy]);

        Option<ReplayAnchor> anchor = testee.Decide(frame, true, null);

        Assert.True(anchor.TryGetValue(out ReplayAnchor value));
        Assert.Equal(firstAnchor, value);
        Assert.Single(firstStrategy.Calls);
        Assert.Single(secondStrategy.Calls);
        Assert.Equal(frame, firstStrategy.Calls[0].Frame);
        Assert.Equal(frame, secondStrategy.Calls[0].Frame);
        Assert.Equal(1, firstStrategy.ResetCount);
        Assert.Equal(1, secondStrategy.ResetCount);
    }

    [Fact]
    public void Decide_returns_none_when_no_strategy_suggests_replay()
    {
        RecordingReplayDecisionStrategy firstStrategy = new(Option<ReplayAnchor>.None());
        RecordingReplayDecisionStrategy secondStrategy = new(Option<ReplayAnchor>.None());
        ReplayDecider testee = new([firstStrategy, secondStrategy]);

        Option<ReplayAnchor> anchor = testee.Decide(new Frame(1, 1_000_000_000), false, null);

        Assert.True(anchor.IsNone);
        Assert.Equal(0, firstStrategy.ResetCount);
        Assert.Equal(0, secondStrategy.ResetCount);
    }

    [Fact]
    public void Update_table_config_updates_all_strategies()
    {
        RecordingReplayDecisionStrategy firstStrategy = new(Option<ReplayAnchor>.None());
        RecordingReplayDecisionStrategy secondStrategy = new(Option<ReplayAnchor>.None());
        ReplayDecider testee = new([firstStrategy, secondStrategy]);

        testee.UpdateTableConfig(TableConfig.Config);

        Assert.Equal([TableConfig.Config], firstStrategy.TableUpdates);
        Assert.Equal([TableConfig.Config], secondStrategy.TableUpdates);
    }

    private static ReplayAnchor CreateAnchor(ulong frameId)
    {
        return new ReplayAnchor(
            new Frame(frameId, (long)frameId * 1_000_000L),
            new Point(frameId, frameId + 1),
            BallPossession.None,
            0,
            ReplayTriggerKind.BallDisappeared);
    }

    private class RecordingReplayDecisionStrategy : IReplayDecisionStrategy
    {
        private readonly Option<ReplayAnchor> _Result;

        public RecordingReplayDecisionStrategy(Option<ReplayAnchor> result)
        {
            _Result = result;
        }

        public List<ReplayDecisionCall> Calls { get; } = [];

        public List<TableConfiguration> TableUpdates { get; } = [];

        public int ResetCount { get; private set; }

        public Option<ReplayAnchor> Decide(Frame frame, bool isBallObserved, ReplayCandidate? candidate)
        {
            Calls.Add(new ReplayDecisionCall(frame, isBallObserved, candidate));
            return _Result;
        }

        public void UpdateTableConfig(TableConfiguration tableConfig)
        {
            TableUpdates.Add(tableConfig);
        }

        public void Reset()
        {
            ResetCount++;
        }
    }

    private readonly record struct ReplayDecisionCall(
        Frame Frame,
        bool IsBallObserved,
        ReplayCandidate? Candidate);
}
