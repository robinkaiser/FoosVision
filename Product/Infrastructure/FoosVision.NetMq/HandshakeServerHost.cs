// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.NetMq.Internal;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Handshake;
using MessagePack;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

/// <summary>
/// Binds a REP socket and serves handshake requests in a single loop.
/// </summary>
public class HandshakeServerHost : IDisposable
{
    private readonly ResponseSocket _Socket;
    private readonly IHandshakeHandler _Handler;
    private readonly CancellationTokenSource _Cts = new();

    // REP receive loop poll interval.
    private readonly TimeSpan _PollTimeout = TimeSpan.FromMilliseconds(250);

    private Task? _LoopTask;

    public HandshakeServerHost(IHandshakeHandler handler)
    {
        _Handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _Socket = new ResponseSocket();

        // _socket.Options.Linger = TimeSpan.Zero;
    }

    public void Start(string bindAddress)
    {
        if (_LoopTask is not null)
        {
            throw new InvalidOperationException("Handshake server already started.");
        }

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
        { /* ignore */
        }

        _Socket.Dispose();
        _Cts.Dispose();
    }

    private void Loop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!Frames.TryReceive(out var type, out var payload, _Socket, _PollTimeout))
            {
                continue;
            }

            if ((HandshakeMessageType)type != HandshakeMessageType.HelloRequest)
            {
                // Minimal behavior: reply with a protocol-level error could be added later.
                // For now, reply with a HelloResponse to keep the REQ/REP state machine valid.
                var fallback = new HelloResponse
                {
                    ProtocolVersion = ProtocolVersions.Current,
                    RecorderAppVersion = "0.0.42",
                };

                var fallbackBytes = MessagePackSerializer.Serialize(fallback, cancellationToken: ct);
                Frames.Send((byte)HandshakeMessageType.HelloResponse, fallbackBytes, _Socket);

                continue;
            }

            var request = MessagePackSerializer.Deserialize<HelloRequest>(payload, cancellationToken: ct);
            var response = _Handler.Handle(request);

            var responseBytes = MessagePackSerializer.Serialize(response, cancellationToken: ct);
            Frames.Send((byte)HandshakeMessageType.HelloResponse, responseBytes, _Socket);
        }
    }
}
