// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Common;
using MessagePack;

namespace FoosVision.Protocol.Messages.Handshake;

[MessagePackObject(true)]
public record HelloRequest
{
    public int ProtocolVersion { get; init; } = ProtocolVersions.Current;

    public string ViewerIpAddress { get; init; } = string.Empty;
}
