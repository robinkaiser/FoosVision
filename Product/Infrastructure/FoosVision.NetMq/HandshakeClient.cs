// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.NetMq.Internal;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Handshake;
using MessagePack;
using NetMQ.Sockets;

namespace FoosVision.NetMq;

public class HandshakeClient : IHandshakeClient, IDisposable
{
    private readonly SemaphoreSlim _Mutex = new(1, 1);

    // Keep a modest default timeout; caller can wrap with CancellationToken timeout if desired.
    private readonly TimeSpan _IoTimeout = TimeSpan.FromSeconds(2);

    public HandshakeClient()
    {
    }

    public Task<HelloResponse> HelloAsync(string recorderReqRepAddress, HelloRequest request, CancellationToken ct)
        => Task.Run(() => HelloBlocking(recorderReqRepAddress, request, ct), ct);

    public void Dispose()
    {
        _Mutex.Dispose();
    }

    private HelloResponse HelloBlocking(string address, HelloRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        _Mutex.Wait(ct);

        try
        {
            using RequestSocket socket = new();
            socket.Options.Linger = TimeSpan.Zero;

            socket.Connect(address);

            var payload = MessagePackSerializer.Serialize(request, cancellationToken: ct);

            Frames.Send((byte)HandshakeMessageType.HelloRequest, payload, socket);

            if (!Frames.TryReceive(out var msgType, out var replyPayload, socket, _IoTimeout))
            {
                throw new TimeoutException($"Handshake timed out waiting for reply from {address}.");
            }

            if ((HandshakeMessageType)msgType != HandshakeMessageType.HelloResponse)
            {
                throw new InvalidOperationException($"Unexpected handshake reply type: {msgType}.");
            }

            var response = MessagePackSerializer.Deserialize<HelloResponse>(replyPayload, cancellationToken: ct);

            return response;
        }
        finally
        {
            _Mutex.Release();
        }
    }
}
