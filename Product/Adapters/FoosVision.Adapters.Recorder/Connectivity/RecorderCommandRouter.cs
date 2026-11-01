// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Game.Control;
using FoosVision.Adapters.Recorder.Setup.Control;
using FoosVision.Adapters.Recorder.Training.Control;
using FoosVision.Common.Logging;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Common;

namespace FoosVision.Adapters.Recorder.Connectivity;

public class RecorderCommandRouter : IRecorderCommandRouter
{
    private static readonly Source _Log = new("RecorderCommandRouter");
    private readonly SemaphoreSlim _Commands = new(1, 1);
    private readonly SetupCommandHandler _Setup;
    private readonly GameCommandHandler _Game;
    private readonly TrainingCommandHandler _Training;

    public RecorderCommandRouter(
        SetupCommandHandler setup,
        GameCommandHandler game,
        TrainingCommandHandler training)
    {
        _Setup = setup;
        _Game = game;
        _Training = training;
    }

    public ValueTask<CommandResponse> DispatchAsync(CommandMessageType type, object command, CancellationToken ct)
    {
        return type switch
        {
            CommandMessageType.StartSetup => Dispatch<StartSetupCommand>(command, _Setup.Handle, ct),
            CommandMessageType.StopSetup => Dispatch<StopSetupCommand>(command, _Setup.Handle, ct),

            CommandMessageType.StartGame => Dispatch<StartGameCommand>(command, _Game.Handle, ct),
            CommandMessageType.StopGame => Dispatch<StopGameCommand>(command, _Game.Handle, ct),

            CommandMessageType.StartTraining => Dispatch<StartTrainingCommand>(command, _Training.Handle, ct),
            CommandMessageType.StopTraining => Dispatch<StopTrainingCommand>(command, _Training.Handle, ct),

            _ => new ValueTask<CommandResponse>(Unsupported(type)),
        };
    }

    private ValueTask<CommandResponse> Dispatch<TCommand>(
        object command,
        Func<TCommand, CancellationToken, Task> handler,
        CancellationToken ct)
        where TCommand : class, ICommand
    {
        if (command is not TCommand cmd)
        {
            return new ValueTask<CommandResponse>(BadPayload(typeof(TCommand).Name, command));
        }

        _ = ExecuteCommand(() => handler(cmd, ct), ct);

        return new ValueTask<CommandResponse>(Accepted(cmd.CommandId));
    }

    public async Task RunExclusive(Func<Task> action)
    {
        await _Commands.WaitAsync(CancellationToken.None);

        try
        {
            await action();
        }
        finally
        {
            _Commands.Release();
        }
    }

    private async Task ExecuteCommand(Func<Task> command, CancellationToken ct)
    {
        try
        {
            await _Commands.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                await command();
            }
            finally { _Commands.Release(); }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _Log.Error("Recorder command failed. Ex={Exception}", ex.ToString());
        }
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
