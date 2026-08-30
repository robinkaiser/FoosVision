// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Recorder-side port: dispatches inbound commands to application controllers (REQ/REP).
/// </summary>
public interface IRecorderCommandRouter
{
    ValueTask<CommandResponse> DispatchAsync(CommandMessageType type, object payload, CancellationToken ct);
}
