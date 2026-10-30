// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Services.ReplayDecision;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Game.Services.GameTracking;

public class GameTracker : IGameTracker
{
    private readonly LiveTracker _LiveTracker;
    private readonly IReplayDecider _ReplayDecider;

    public GameTracker(LiveTracker liveTracker, IReplayDecider replayDecider)
    {
        _LiveTracker = liveTracker;
        _ReplayDecider = replayDecider;
    }

    public GameTrackingSnapshot? Latest { get; private set; }

    public GameTrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
    {
        LiveTrackingSnapshot tracking = _LiveTracker.ObserveFrame(frame, observations);
        ReplayCandidate? candidate = null;
        Frame decisionFrame = frame;

        if (tracking.IsBallFound)
        {
            TrackedBall observed = tracking.BallCandidates.First(static c => c.Status == TrackingStatus.Observed);
            decisionFrame = observed.Frame;

            if (tracking.ClosestBar is BarType bar)
            {
                candidate = new ReplayCandidate(observed.Frame, observed.Position, tracking.Possession,
                    tracking.PossessionTimeMs, observed.VelocityPxPerS, observed.Confidence, bar);
            }
        }

        ReplayAnchor? replayAnchor = _ReplayDecider.Decide(decisionFrame, tracking.IsBallFound, candidate)
            .TryGetValue(out ReplayAnchor? anchor) ? anchor : null;

        // A replay suggestion has always taken precedence over releasing expired possession.
        if (replayAnchor == null)
        {
            _LiveTracker.CompleteFrame();
        }

        Latest = GameTrackingSnapshotFactory.Create(tracking, replayAnchor);
        return Latest;
    }

    public void UpdateTableConfig(TableConfiguration tableConfig)
    {
        _LiveTracker.UpdateTableConfig(tableConfig);
        _ReplayDecider.UpdateTableConfig(tableConfig);
    }
}
