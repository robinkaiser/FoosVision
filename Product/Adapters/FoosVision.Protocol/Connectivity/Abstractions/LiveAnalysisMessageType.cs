// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Recorder-to-viewer analysis messages that may carry larger payloads than continuous live data.
/// This may include large video segments for in-depth replay analysis.
/// </summary>
public enum LiveAnalysisMessageType : byte
{
    ReplayStarted = 1,
    Replay = 2,
    VisionContext = 3,
    BallDetectionMask = 4,
}
