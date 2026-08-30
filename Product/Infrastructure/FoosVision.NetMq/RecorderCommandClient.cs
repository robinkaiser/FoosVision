// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.NetMq.Internal;
using FoosVision.Protocol;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Common;
using MessagePack;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

public class RecorderCommandClient : IRecorderCommandClient, IDisposable
{
    private readonly string _RecorderCommandsAddress;
    private readonly RequestSocket _Socket = new();
    private readonly SemaphoreSlim _Mutex = new(1, 1);
    private readonly TimeSpan _IoTimeout;

    public RecorderCommandClient(string recorderCommandsAddress, TimeSpan? ioTimeout = null)
    {
        _RecorderCommandsAddress = recorderCommandsAddress;
        _IoTimeout = ioTimeout ?? TimeSpan.FromSeconds(2);

        _Socket.Connect(_RecorderCommandsAddress);
    }

    public void Dispose()
    {
        _Socket.Dispose();
        _Mutex.Dispose();
    }

    public Task<CommandResponse> SendAsync<TCommand>(TCommand cmd, CancellationToken ct)
        => Task.Run(() => SendBlocking(cmd, ct), ct);

    private CommandResponse SendBlocking<TCommand>(TCommand cmd, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _Mutex.Wait(ct);
        try
        {
            var cmdType = ProtocolTypeRegistry.GetCommandType<TCommand>();
            var payload = MessagePackSerializer.Serialize(cmd, cancellationToken: ct);

            Frames.Send((byte)cmdType, payload, _Socket);

            if (!Frames.TryReceive(out var replyTypeByte, out var replyPayload, _Socket, _IoTimeout))
            {
                throw new TimeoutException($"Command timed out waiting for reply from {_RecorderCommandsAddress}.");
            }

            var replyType = (CommandReplyMessageType)replyTypeByte;
            if (replyType != CommandReplyMessageType.CommandResponse)
            {
                throw new InvalidOperationException($"Unexpected command reply type: {replyType}.");
            }

            return MessagePackSerializer.Deserialize<CommandResponse>(replyPayload, cancellationToken: ct);
        }
        finally
        {
            _Mutex.Release();
        }
    }
}
