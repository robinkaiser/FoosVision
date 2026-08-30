// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Common;
using MessagePack;

namespace FoosVision.Protocol.Messages.Handshake;

[MessagePackObject(true)]
public record HelloResponse
{
    public int ProtocolVersion { get; init; } = ProtocolVersions.Current;

    public string RecorderAppVersion { get; init; } = string.Empty;

    public bool Accepted { get; init; } = true;

    public string RejectionReason { get; init; } = string.Empty;

    public HandshakeDiagnosticsSettings Diagnostics { get; init; } = new();

    public HandshakeViewerSettings Viewer { get; init; } = new();
}
