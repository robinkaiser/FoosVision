// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Recorder-side port: publishes larger live analysis messages to connected viewers (PUB/SUB).
/// </summary>
public interface IRecorderLiveAnalysisPublisher
{
    Task PublishReplayStarted(ReplayStartedMessage replayStarted, CancellationToken ct = default);

    Task PublishReplay(ReplayMessage replay, CancellationToken ct = default);

    Task PublishVisionContext(VisionContextMessage visionContext, CancellationToken ct = default);

    Task PublishBallDetectionMask(BallDetectionMaskMessage ballDetectionMask, CancellationToken ct = default);
}
