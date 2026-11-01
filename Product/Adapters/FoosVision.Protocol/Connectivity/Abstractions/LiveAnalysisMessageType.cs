// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Recorder-to-viewer analysis messages that may carry larger payloads than continuous live data.
/// This may include large video segments for in-depth replay analysis.
/// </summary>
public enum LiveAnalysisMessageType : byte
{
    VisionContext = 1,
    BallDetectionMask = 2,
    ReplayStarted = 3,
    ReplayClip = 4,
    ReplayReturnToLive = 5,
}
