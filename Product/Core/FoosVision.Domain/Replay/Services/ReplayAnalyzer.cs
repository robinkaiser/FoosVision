// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Logging;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Replay.Services;

public class ReplayAnalyzer : IReplayAnalyzer
{
    private enum ShotState
    {
        Idle,
        Candidate,
        Confirmed,
    }

    private record struct FrameStatistics(
        int AnchorFramesCount,
        int TrackedFramesCount,
        int PredictedFramesCount,
        int MissingFramesCount);

    private readonly record struct SpeedFrame(long TimeNs, Point Position);

    public const string GoalSpeedMetricName = "Goal Speed";
    public const string SideSpeedMetricName = "Side Speed";
    public const string ShotDurationMetricName = "Shot Duration";

    private const long _VelocityAverageWindowNs = 34L * 1_000_000L;
    private const double _ShotCandidateVelocityThresholdPxPerS = 200.0;
    private const double _ShotConfirmationSideMovementThresholdPx = 50.0;
    private const int _ShotCandidateDisplayThresholdMs = 25;

    private static readonly Source _Log = new("ReplayAnalyzer");

    private readonly TableImageScale _TableImageScale;

    public ReplayAnalyzer(TableImageScale tableImageScale)
    {
        _TableImageScale = tableImageScale;
    }

    public ReplayAnalysis Analyze(IEnumerable<ReplayTrackedFrame> trackedFrames)
    {
        List<ReplayAnalysisFrame> frames = [];
        Queue<SpeedFrame> recentSpeedFrames = [];

        BallPossession shotAnchorPossession = BallPossession.None;
        FrameStatistics frameStatistics = new(0, 0, 0, 0);

        double goalSpeed = 0.0;
        double sideSpeed = 0.0;

        var internalShotDuration = TimeSpan.Zero;
        var publishedShotDuration = TimeSpan.Zero;
        var shotState = ShotState.Idle;
        var shotStartTimeNs = 0L;
        var candidateStartPosition = default(Point);
        var candidateStartSideDirection = 0;

        foreach (ReplayTrackedFrame frame in trackedFrames.OrderBy(f => f.TimeNs))
        {
            CountFrameStatistics(frame.Status, ref frameStatistics);

            if (frame.Status == ReplayTrackedFrameStatus.Anchor)
            {
                shotAnchorPossession = frame.Possession;
            }

            var windowVelocity = Vector2.Zero;
            bool hasVelocity = TryGetPositionVelocities(
                frame,
                recentSpeedFrames,
                out windowVelocity);

            if (hasVelocity)
            {
                goalSpeed = Math.Max(goalSpeed, Math.Abs(windowVelocity.X));
                sideSpeed = Math.Max(sideSpeed, Math.Abs(windowVelocity.Y));
            }

            if (frame.Status != ReplayTrackedFrameStatus.Missing)
            {
                bool isStillInAnchorPossession = frame.Possession == shotAnchorPossession;

                internalShotDuration = AdvanceShotDuration(
                    frame.TimeNs,
                    frame.BallPosition,
                    windowVelocity,
                    isStillInAnchorPossession,
                    ref shotState,
                    ref shotStartTimeNs,
                    ref candidateStartPosition,
                    ref candidateStartSideDirection);

                bool isObserved =
                    frame.Status == ReplayTrackedFrameStatus.Anchor ||
                    frame.Status == ReplayTrackedFrameStatus.Tracked;

                if (isObserved)
                {
                    publishedShotDuration = internalShotDuration;
                }
            }

            var metrics = CreateMetrics(goalSpeed, sideSpeed, publishedShotDuration);
            var analysisFrame = CreateAnalysisFrame(frame, metrics);

            frames.Add(analysisFrame);
        }

        _Log.Information(
            "Replay analysis completed. Frames={0} Anchor={1} Tracked={2} Predicted={3} Missing={4}",
            frames.Count,
            frameStatistics.AnchorFramesCount,
            frameStatistics.TrackedFramesCount,
            frameStatistics.PredictedFramesCount,
            frameStatistics.MissingFramesCount);

        return new ReplayAnalysis(frames);
    }

    private static bool TryGetPositionVelocities(
        ReplayTrackedFrame frame,
        Queue<SpeedFrame> recentSpeedFrames,
        out Vector2 windowVelocity)
    {
        // Replay speed intentionally ignores tracker object identity and derives velocity from positions.
        // At high shot speeds the 120 fps frames can show the ball as a motion-blurred streak, causing
        // the current tracker to create a new ball and reset its velocity to zero. This keeps RC metrics
        // useful; long term, replay needs a high-speed observation and tracking strategy for blurred balls.
        windowVelocity = Vector2.Zero;

        if (frame.Status == ReplayTrackedFrameStatus.Missing)
        {   // A missing frame has no new position, so it must not enqueue a zero velocity into the average.
            return false;
        }

        SpeedFrame currentFrame = new(frame.TimeNs, frame.BallPosition);

        recentSpeedFrames.Enqueue(currentFrame);

        long minTimeNs = frame.TimeNs - _VelocityAverageWindowNs;

        while (recentSpeedFrames.Count > 0 && recentSpeedFrames.Peek().TimeNs < minTimeNs)
        {
            recentSpeedFrames.Dequeue();
        }

        if (recentSpeedFrames.Count < 2)
        {
            return false;
        }

        windowVelocity = GetVelocity(recentSpeedFrames.Peek(), currentFrame);
        return true;
    }

    private static Vector2 GetVelocity(SpeedFrame fromFrame, SpeedFrame toFrame)
    {
        double secondsElapsed = (toFrame.TimeNs - fromFrame.TimeNs) / 1_000_000_000.0;

        if (secondsElapsed <= 0.0)
        {
            return Vector2.Zero;
        }

        return new Vector2(
            (toFrame.Position.X - fromFrame.Position.X) / secondsElapsed,
            (toFrame.Position.Y - fromFrame.Position.Y) / secondsElapsed);
    }

    private static TimeSpan AdvanceShotDuration(
        long timeNs,
        Point ballPosition,
        Vector2 velocity,
        bool isStillInAnchorPossession,
        ref ShotState shotState,
        ref long shotStartTimeNs,
        ref Point candidateStartPosition,
        ref int candidateStartSideDirection)
    {
        switch (shotState)
        {
            case ShotState.Idle:
                if (Math.Abs(velocity.X) >= _ShotCandidateVelocityThresholdPxPerS ||
                    Math.Abs(velocity.Y) >= _ShotCandidateVelocityThresholdPxPerS)
                {   // Ball is no longer stationary, so this may be the start of a shot.
                    // TODO: missing initial frame time (e.g. 8.333 ms for 120 fps)
                    shotState = ShotState.Candidate;
                    shotStartTimeNs = timeNs;
                    candidateStartPosition = ballPosition;
                    candidateStartSideDirection = GetDirection(velocity.Y);
                }
                return TimeSpan.Zero;

            case ShotState.Candidate:
                int currentSideDirection = GetDirection(velocity.Y);

                if (!isStillInAnchorPossession)
                {   // Ball left anchor rod, so the shot candidate is confirmed.
                    shotState = ShotState.Confirmed;
                    return GetElapsedSince(shotStartTimeNs, timeNs);
                }

                if (currentSideDirection != candidateStartSideDirection)
                {   // Ball is moving to the opposite direction (pull <=> push) or standing still, so discard the candidate.
                    shotState = ShotState.Idle;
                    return TimeSpan.Zero;
                }

                double sideMovementPx = Math.Abs(ballPosition.Y - candidateStartPosition.Y);

                if (sideMovementPx >= _ShotConfirmationSideMovementThresholdPx)
                {   // Ball moved far enough in one side direction, so the shot candidate is confirmed.
                    shotState = ShotState.Confirmed;
                    return GetElapsedSince(shotStartTimeNs, timeNs);
                }

                // Keep very short candidates hidden until they are confirmed or visible long enough.
                TimeSpan candidateDuration = GetElapsedSince(shotStartTimeNs, timeNs);

                return candidateDuration.TotalMilliseconds < _ShotCandidateDisplayThresholdMs ?
                    TimeSpan.Zero :
                    candidateDuration;

            case ShotState.Confirmed:
                return GetElapsedSince(shotStartTimeNs, timeNs);

            default:
                return TimeSpan.Zero;
        }
    }

    private IReadOnlyList<ReplayMetric> CreateMetrics(
        double goalSpeed,
        double sideSpeed,
        TimeSpan shotDuration)
    {
        return
        [
            new(GoalSpeedMetricName, _TableImageScale.ConvertGoalAxisSpeedPxPerSToKmh(goalSpeed), "km/h"),
            new(SideSpeedMetricName, _TableImageScale.ConvertSideAxisSpeedPxPerSToKmh(sideSpeed), "km/h"),
            new(ShotDurationMetricName, shotDuration.TotalMilliseconds, "ms"),
        ];
    }

    private static int GetDirection(double velocity)
    {
        if (velocity == 0) return 0;
        return velocity > 0 ? 1 : -1;
    }

    private static TimeSpan GetElapsedSince(long t0, long t1)
        => TimeSpan.FromTicks((t1 - t0) / 100);

    private static ReplayAnalysisFrame CreateAnalysisFrame(
        ReplayTrackedFrame frame,
        IReadOnlyList<ReplayMetric> metrics)
    {
        Option<Point> ballPosition = frame.Status == ReplayTrackedFrameStatus.Missing
            ? Option<Point>.None()
            : Option<Point>.Some(frame.BallPosition);

        return new ReplayAnalysisFrame(frame.TimeNs, ballPosition, frame.Possession, metrics);
    }

    private static void CountFrameStatistics(ReplayTrackedFrameStatus status, ref FrameStatistics statistics)
    {
        switch (status)
        {
            case ReplayTrackedFrameStatus.Anchor:
                statistics.AnchorFramesCount++;
                break;
            case ReplayTrackedFrameStatus.Tracked:
                statistics.TrackedFramesCount++;
                break;
            case ReplayTrackedFrameStatus.Predicted:
                statistics.PredictedFramesCount++;
                break;
            case ReplayTrackedFrameStatus.Missing:
                statistics.MissingFramesCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }
    }
}
