// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Live;

[MessagePackObject(true)]
public record TrackingFrameMessage
{
    public ulong FrameId { get; init; }

    public long TimestampNs { get; init; }

    public bool IsBallFound { get; init; }

    public PointMessage? BallPosition { get; init; }

    public VectorMessage BallVelocityPxPerS { get; init; } = new();

    public List<TrackedBallCandidateMessage> BallCandidates { get; init; } = [];

    public List<ObservedBallMessage> Observations { get; init; } = [];

    public PossessionMessage Possession { get; init; } = PossessionMessage.None;

    public int PossessionTimeMs { get; init; }

    public bool IsTimeFoul { get; init; }
}

public enum TrackingStatusMessage
{
    Observed,
    Predicted,
}

public enum TrackingConfidenceMessage
{
    Low,
    Average,
    High,
}

public enum TrackingEvidenceMessage
{
    Prediction,
    LowQualityObservation,
    HighQualityObservation,
    VeryHighQualityObservation,
}

public enum ObservationQualityLevelMessage
{
    BelowMinimum,
    LowQuality,
    HighQuality,
    VeryHighQuality,
}

[MessagePackObject(true)]
public record ObservedBallMessage
{
    public PointMessage Position { get; init; } = new();

    public ObservationQualityLevelMessage QualityLevel { get; init; }
}

[MessagePackObject(true)]
public record TrackedBallCandidateMessage
{
    public PointMessage Position { get; init; } = new();

    public TrackingStatusMessage Status { get; init; }

    public TrackingConfidenceMessage Confidence { get; init; }

    public TrackingEvidenceMessage Evidence { get; init; }

    public int UnobservedAgeMs { get; init; }
}
