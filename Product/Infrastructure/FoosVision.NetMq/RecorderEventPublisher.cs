// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol;
using FoosVision.Protocol.Connectivity.Abstractions;
using MessagePack;
using NetMQ;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

public class RecorderEventPublisher : IRecorderEventPublisher, IDisposable
{
    private readonly PublisherSocket _Socket = new();
    private readonly Lock _SocketLock = new();

    public void Bind(string bindAddress)
    {
        _Socket.Bind(bindAddress);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _Socket.Dispose();
    }

    public Task PublishAsync<TEvent>(TEvent evt, CancellationToken ct)
    {
        var type = ProtocolTypeRegistry.GetEventType<TEvent>();
        var payload = MessagePackSerializer.Serialize(evt, cancellationToken: ct);

        lock (_SocketLock)
        {
            _Socket.SendMoreFrame([(byte)type]);
            _Socket.SendFrame(payload);
        }

        return Task.CompletedTask;
    }
}
