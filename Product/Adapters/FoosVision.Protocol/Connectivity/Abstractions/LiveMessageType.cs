// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Continuous or replaceable recorder-to-viewer data stream messages.
/// Use this channel for ongoing processing/display data rather than discrete workflow events.
/// </summary>
public enum LiveMessageType : byte
{
    TrackingFrame = 1,
    TableUpdate = 2,
}
