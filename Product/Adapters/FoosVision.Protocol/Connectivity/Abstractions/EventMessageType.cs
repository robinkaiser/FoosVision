// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Discrete recorder-to-viewer messages for runtime/workflow relevant changes.
/// These are not the continuous live-data stream and should stay semantically event-like.
/// </summary>
public enum EventMessageType : byte
{
    RecorderRuntimeStateChanged = 0,
}
