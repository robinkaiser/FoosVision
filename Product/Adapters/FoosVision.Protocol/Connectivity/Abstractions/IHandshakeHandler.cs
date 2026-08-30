// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Handshake;

namespace FoosVision.Protocol.Connectivity.Abstractions;

public interface IHandshakeHandler
{
    HelloResponse Handle(HelloRequest request);
}
