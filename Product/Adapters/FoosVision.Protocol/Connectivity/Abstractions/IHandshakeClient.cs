// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Handshake;

namespace FoosVision.Protocol.Connectivity.Abstractions;

public interface IHandshakeClient
{
    /// <summary>
    /// Performs a handshake against a recorder REP endpoint (e.g. "tcp://192.168.1.50:5555").
    /// </summary>
    /// <param name="recorderReqRepAddress">Endpoint.</param>
    /// <param name="request">Handshake request.</param>
    /// <param name="ct">Token.</param>
    /// <returns>Hello response.</returns>
    Task<HelloResponse> HelloAsync(string recorderReqRepAddress, HelloRequest request, CancellationToken ct);
}
