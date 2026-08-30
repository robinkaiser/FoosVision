// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Game.Control;
using FoosVision.Adapters.Recorder.Installation.Control;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Adapters.Recorder.Connectivity;

public class RecorderCommandRouter : IRecorderCommandRouter
{
    private readonly InstallCommandHandler _Install;
    private readonly GameCommandHandler _Game;

    public RecorderCommandRouter(
        InstallCommandHandler install,
        GameCommandHandler game)
    {
        _Install = install;
        _Game = game;
    }

    public ValueTask<CommandResponse> DispatchAsync(CommandMessageType type, object command, CancellationToken ct)
    {
        return type switch
        {
            CommandMessageType.StartInstall => Dispatch<StartInstallCommand>(command, _Install.Handle, ct),
            CommandMessageType.StopInstall => Dispatch<StopInstallCommand>(command, _Install.Handle, ct),

            CommandMessageType.StartGame => Dispatch<StartGameCommand>(command, _Game.Handle, ct),
            CommandMessageType.StopGame => Dispatch<StopGameCommand>(command, _Game.Handle, ct),

            _ => new ValueTask<CommandResponse>(Unsupported(type)),
        };
    }

    private static ValueTask<CommandResponse> Dispatch<TCommand>(
        object command,
        Func<TCommand, CancellationToken, Task> handler,
        CancellationToken ct)
        where TCommand : class, ICommand
    {
        if (command is not TCommand cmd)
        {
            return new ValueTask<CommandResponse>(BadPayload(typeof(TCommand).Name, command));
        }

        _ = handler(cmd, ct);

        return new ValueTask<CommandResponse>(Accepted(cmd.CommandId));
    }

    private static CommandResponse Accepted(Guid commandId) => new()
    {
        CommandId = commandId,
        Accepted = true,
    };

    private static CommandResponse Unsupported(CommandMessageType type) => new()
    {
        CommandId = Guid.Empty,
        Accepted = false,
        Error = $"Unsupported command type: {type}",
    };

    private static CommandResponse BadPayload(string expected, object? actual) => new()
    {
        CommandId = Guid.Empty,
        Accepted = false,
        Error = $"Invalid payload CLR type for {expected}. Got: {actual?.GetType().FullName ?? "null"}",
    };
}
