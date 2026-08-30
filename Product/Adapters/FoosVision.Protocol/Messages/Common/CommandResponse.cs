// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Common;

[MessagePackObject(true)]
public class CommandResponse
{
    public Guid CommandId { get; init; }

    public bool Accepted { get; init; }

    public string Error { get; init; } = string.Empty;
}
