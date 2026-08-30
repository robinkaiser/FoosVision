// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Adapters.Viewer.Session.Replay;

internal static class ReplayAnalysisMapper
{
    private const float _FrameWidth = 1920f;
    private const float _FrameHeight = 1080f;

    public static TrackingOverlayState Map(
        ReplayAnalysis analysis,
        long timeNs,
        ReplayPossessionOverlay? possessionOverlay = null)
    {
        if (analysis.Frames.Count == 0)
        {
            return new TrackingOverlayState(
                Trail: [],
                BallPosition: null,
                Observations: [],
                BallCandidates: [],
                PossessingTeam: Team.None,
                PossessionArea: PossessionArea.None,
                PossessionTimeMs: 0,
                IsTimeFoul: false,
                Metrics: []);
        }

        int currentFrameIndex = FindCurrentFrameIndex(analysis.Frames, timeNs);
        ReplayAnalysisFrame currentFrame = analysis.Frames[currentFrameIndex];
        BallPossession possession = GetReplayPossession(
            analysis.Frames,
            currentFrameIndex,
            currentFrame,
            possessionOverlay,
            out int possessionTimeMs);
        IReadOnlyList<OverlayPoint> ballPath =
        [
            .. analysis.Frames
                .Take(currentFrameIndex + 1)
                .Where(frame => frame.BallPosition.IsSome)
                .Select(frame => ToOverlayPoint(frame.BallPosition.Value)),
        ];

        return new TrackingOverlayState(
            Trail: ballPath,
            BallPosition: currentFrame.BallPosition.Match(point => (OverlayPoint?)ToOverlayPoint(point), () => null),
            Observations: [],
            BallCandidates: [],
            PossessingTeam: possession.Team,
            PossessionArea: possession.Area,
            PossessionTimeMs: possessionTimeMs,
            IsTimeFoul: false,
            Metrics: ToOverlayMetrics(currentFrame.Metrics));
    }

    private static int FindCurrentFrameIndex(IReadOnlyList<ReplayAnalysisFrame> frames, long timeNs)
    {
        int currentFrameIndex = 0;
        for (int i = 0; i < frames.Count; i++)
        {
            if (frames[i].TimeNs > timeNs)
            {
                break;
            }

            currentFrameIndex = i;
        }

        return currentFrameIndex;
    }

    private static BallPossession GetReplayPossession(
        IReadOnlyList<ReplayAnalysisFrame> frames,
        int currentFrameIndex,
        ReplayAnalysisFrame currentFrame,
        ReplayPossessionOverlay? possessionOverlay,
        out int possessionTimeMs)
    {
        possessionTimeMs = 0;

        if (possessionOverlay is not ReplayPossessionOverlay overlay ||
            overlay.AnchorPossession == BallPossession.None ||
            currentFrame.TimeNs < overlay.AnchorTimestampNs)
        {
            return BallPossession.None;
        }

        long highestAnchorPossessionTimeNs = overlay.AnchorTimestampNs;
        bool hasAnchorPossessionFrame = false;

        for (int i = 0; i <= currentFrameIndex; i++)
        {
            ReplayAnalysisFrame frame = frames[i];
            if (frame.TimeNs < overlay.AnchorTimestampNs)
            {
                continue;
            }

            if (!frame.BallPosition.TryGetValue(out _))
            {
                break;
            }

            if (frame.Possession != overlay.AnchorPossession)
            {
                break;
            }

            highestAnchorPossessionTimeNs = frame.TimeNs;
            hasAnchorPossessionFrame = true;
        }

        if (!hasAnchorPossessionFrame)
        {
            return BallPossession.None;
        }

        possessionTimeMs = AddMilliseconds(
            overlay.AnchorPossessionTimeMs,
            highestAnchorPossessionTimeNs - overlay.AnchorTimestampNs);
        return overlay.AnchorPossession;
    }

    private static int AddMilliseconds(int baseTimeMs, long elapsedNs)
    {
        if (elapsedNs <= 0)
        {
            return Math.Max(0, baseTimeMs);
        }

        long elapsedMs = elapsedNs / 1_000_000L;
        long totalMs = baseTimeMs + elapsedMs;
        return totalMs > int.MaxValue ? int.MaxValue : (int)Math.Max(0, totalMs);
    }

    private static IReadOnlyList<TrackingOverlayMetric> ToOverlayMetrics(IReadOnlyList<ReplayMetric> metrics)
        => [.. metrics.Select(metric => new TrackingOverlayMetric(metric.Name, metric.Value, metric.Unit))];

    private static OverlayPoint ToOverlayPoint(Point point)
    {
        return new OverlayPoint(
            X: Math.Clamp((float)point.X / _FrameWidth, 0f, 1f),
            Y: Math.Clamp((float)point.Y / _FrameHeight, 0f, 1f));
    }
}
