// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Recorder-side port: publishes events to connected viewers (PUB/SUB).
/// </summary>
public interface IRecorderEventPublisher
{
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct);
}
