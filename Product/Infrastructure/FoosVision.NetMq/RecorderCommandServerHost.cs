// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.NetMq.Internal;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Commands;
using FoosVision.Protocol.Messages.Common;
using MessagePack;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

/// <summary>
/// Recorder-side REP server that receives commands and dispatches them into the app.
/// </summary>
public class RecorderCommandServerHost : IDisposable
{
    private readonly ResponseSocket _Socket = new();
    private readonly IRecorderCommandRouter _Dispatcher;
    private readonly CancellationTokenSource _Cts = new();
    private readonly TimeSpan _PollTimeout = TimeSpan.FromMilliseconds(250);
    private Task? _LoopTask;

    public RecorderCommandServerHost(IRecorderCommandRouter dispatcher)
    {
        _Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public void Start(string bindAddress)
    {
        if (_LoopTask is not null) throw new InvalidOperationException("Command server already started.");

        _Socket.Bind(bindAddress);
        _LoopTask = Task.Run(() => Loop(_Cts.Token));
    }

    public void Dispose()
    {
        _Cts.Cancel();

        try
        {
            _LoopTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
        }

        _Socket.Dispose();
        _Cts.Dispose();
    }

    private async Task Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!Frames.TryReceive(out var typeByte, out var payload, _Socket, _PollTimeout))
            {
                continue;
            }

            var cmdType = (CommandMessageType)typeByte;
            CommandResponse response;

            try
            {
                var commandObj = DeserializeCommand(cmdType, payload, ct);
                response = await _Dispatcher.DispatchAsync(cmdType, commandObj, ct);
            }
            catch (Exception ex)
            {
                response = new CommandResponse
                {
                    CommandId = Guid.Empty,
                    Accepted = false,
                    Error = $"Unhandled exception in command dispatcher: {ex.Message}",
                };
            }

            var responseBytes = MessagePackSerializer.Serialize(response, cancellationToken: ct);
            Frames.Send((byte)CommandReplyMessageType.CommandResponse, responseBytes, _Socket);
        }
    }

    private static object DeserializeCommand(CommandMessageType type, byte[] payload, CancellationToken ct)
    {
        return type switch
        {
            CommandMessageType.StartInstall => MessagePackSerializer.Deserialize<StartInstallCommand>(payload, cancellationToken: ct),
            CommandMessageType.StopInstall => MessagePackSerializer.Deserialize<StopInstallCommand>(payload, cancellationToken: ct),

            CommandMessageType.StartGame => MessagePackSerializer.Deserialize<StartGameCommand>(payload, cancellationToken: ct),
            CommandMessageType.StopGame => MessagePackSerializer.Deserialize<StopGameCommand>(payload, cancellationToken: ct),

            _ => throw new NotSupportedException($"Unsupported command type: {type}")
        };
    }
}
