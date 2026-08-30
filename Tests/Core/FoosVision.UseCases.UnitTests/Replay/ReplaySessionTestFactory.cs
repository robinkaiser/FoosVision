// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Replay.Services;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.BallTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.UnitTests;

namespace FoosVision.UseCases.UnitTests.Replay;

internal static class ReplaySessionTestFactory
{
    public static ReplaySession CreateStarted(
        ReplayId replayId,
        int requiredCompletedLoops = ReplaySession.DefaultRequiredCompletedLoops)
    {
        ReplaySession session = new(new NoOpBallTracker(), new NoOpReplayAnalyzer(), requiredCompletedLoops);
        session.Start(
            replayId,
            new ReplayTrackAnchor(new Frame(40, 1_000_000_000), new Point(100, 200)),
            TableConfig.Config);
        return session;
    }

    public static ReplaySession CreateStartedWithObservationTracking(ReplayId replayId)
    {
        ReplaySession session = new(new ObservationBallTracker(), new NoOpReplayAnalyzer());
        session.Start(
            replayId,
            new ReplayTrackAnchor(new Frame(40, 1_000_000_000), new Point(100, 200)),
            TableConfig.Config);
        return session;
    }

    private sealed class NoOpBallTracker : IBallTracker
    {
        public TrackingSnapshot? Latest { get; private set; }

        public TrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
        {
            Latest = new TrackingSnapshot(frame, []);
            return Latest;
        }

        public void UpdateTableConfig(TableConfiguration tableConfig)
        {
        }
    }

    private sealed class ObservationBallTracker : IBallTracker
    {
        public TrackingSnapshot? Latest { get; private set; }

        public TrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations)
        {
            ObservedBall? observation = observations.FirstOrDefault();
            TrackedBall? trackedBall = observation == null
                ? null
                : new TrackedBall(1, frame, observation.Position, TrackingConfidence.High, TrackingStatus.Observed, Vector2.Zero);

            Latest = trackedBall == null
                ? new TrackingSnapshot(frame, [])
                : new TrackingSnapshot(frame, [trackedBall]);
            return Latest;
        }

        public void UpdateTableConfig(TableConfiguration tableConfig)
        {
        }
    }

    private sealed class NoOpReplayAnalyzer : IReplayAnalyzer
    {
        public ReplayAnalysis Analyze(IEnumerable<ReplayTrackedFrame> trackedFrames)
        {
            return new ReplayAnalysis(
                [.. trackedFrames.Select(frame => new ReplayAnalysisFrame(
                    frame.TimeNs,
                    Option<Point>.Some(frame.BallPosition),
                    frame.Possession,
                    []))]);
        }
    }
}
