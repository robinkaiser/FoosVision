// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using NetMQ;

namespace FoosVision.NetMq.Internal;

internal static class Frames
{
    public static void Send(byte type, byte[] payload, NetMQSocket socket)
    {
        socket.SendMoreFrame([type]);
        socket.SendFrame(payload);
    }

    public static bool TryReceive(out byte type, out byte[] payload, NetMQSocket socket, TimeSpan timeout)
    {
        type = default;
        payload = [];

        if (!socket.TryReceiveFrameBytes(timeout, out var typeFrame) ||
            typeFrame is null ||
            typeFrame.Length != 1)
        {
            return false;
        }

        if (!socket.TryReceiveFrameBytes(timeout, out var payloadFrame) ||
            payloadFrame is null)
        {
            return false;
        }

        type = typeFrame[0];
        payload = payloadFrame;
        return true;
    }
}
