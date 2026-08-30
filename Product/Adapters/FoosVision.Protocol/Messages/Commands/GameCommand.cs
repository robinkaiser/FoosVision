// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Commands;

[MessagePackObject(true)]
public record StartGameCommand : ICommand
{
    public Guid CommandId { get; init; }
}

[MessagePackObject(true)]
public record StopGameCommand : ICommand
{
    public Guid CommandId { get; init; }
}
