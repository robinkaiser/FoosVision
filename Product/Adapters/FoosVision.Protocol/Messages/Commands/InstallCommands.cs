// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using MessagePack;

namespace FoosVision.Protocol.Messages.Commands;

[MessagePackObject(true)]
public record StartInstallCommand : ICommand
{
    public Guid CommandId { get; init; }
}

[MessagePackObject(true)]
public record StopInstallCommand : ICommand
{
    public Guid CommandId { get; init; }
}
