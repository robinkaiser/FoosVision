// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Recorder-side port: publishes live data events to connected viewers (PUB/SUB).
/// This is separate from the command/event channel because the semantics differ:
/// high-frequency tracking frames and current table state for live viewing / viewer-side processing.
/// </summary>
public interface IRecorderLiveDataPublisher
{
    Task PublishTrackingFrame(TrackingFrameMessage frame, CancellationToken ct = default);

    Task PublishTableUpdate(TableUpdateMessage update, CancellationToken ct = default);
}
