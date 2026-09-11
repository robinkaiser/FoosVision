// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Game.Control;
using FoosVision.Adapters.Recorder.Setup.Control;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Adapters.Recorder.Connectivity;

public class RecorderCommandRouter : IRecorderCommandRouter
{
    private readonly SetupCommandHandler _Setup;
    private readonly GameCommandHandler _Game;

    public RecorderCommandRouter(
        SetupCommandHandler setup,
        GameCommandHandler game)
    {
        _Setup = setup;
        _Game = game;
    }

    public ValueTask<CommandResponse> DispatchAsync(CommandMessageType type, object command, CancellationToken ct)
    {
        return type switch
        {
            CommandMessageType.StartSetup => Dispatch<StartSetupCommand>(command, _Setup.Handle, ct),
            CommandMessageType.StopSetup => Dispatch<StopSetupCommand>(command, _Setup.Handle, ct),

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
