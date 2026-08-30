// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Protocol.Connectivity.Abstractions;

/// <summary>
/// Viewer-side port: sends commands to a recorder command endpoint (REQ/REP).
/// </summary>
public interface IRecorderCommandClient
{
    Task<CommandResponse> SendAsync<TCommand>(TCommand cmd, CancellationToken ct);
}
