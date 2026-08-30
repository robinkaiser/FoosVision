// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Common;

namespace RecorderCli;

public sealed class RecorderCommandRouter : IRecorderCommandRouter
{
    private readonly LastCommandTracker _LastCommandTracker;

    public RecorderCommandRouter(LastCommandTracker lastCommandTracker)
    {
        _LastCommandTracker = lastCommandTracker;
    }

    public ValueTask<CommandResponse> DispatchAsync(CommandMessageType type, object command, CancellationToken ct)
    {
        if (command is not ICommand cmd)
        {
            return new ValueTask<CommandResponse>(new CommandResponse
            {
                CommandId = Guid.Empty,
                Accepted = false,
                Error = $"Invalid command payload CLR type: {command?.GetType().FullName ?? "null"}",
            });
        }

        _LastCommandTracker.Remember(cmd.CommandId);

        Console.WriteLine($"[CMD] {DateTime.Now:HH:mm:ss.fff} {type} CommandId={cmd.CommandId}");

        return new ValueTask<CommandResponse>(new CommandResponse
        {
            CommandId = cmd.CommandId,
            Accepted = true,
        });
    }
}
