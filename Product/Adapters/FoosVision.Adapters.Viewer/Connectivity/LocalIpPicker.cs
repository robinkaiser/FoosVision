// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Net;
using System.Net.Sockets;

namespace FoosVision.Adapters.Viewer.Connectivity;

internal static class LocalIpPicker
{
    public static IPAddress PickLocalIPv4ForRemote(IPAddress recorderIp)
    {
        // Using UDP "connect" doesn't send packets; it just selects a route/interface.
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        // Any port works; 9 is discard. We only care about chosen local endpoint.
        socket.Connect(new IPEndPoint(recorderIp, 9));

        var local = (IPEndPoint)socket.LocalEndPoint!;

        return local.Address;
    }
}
