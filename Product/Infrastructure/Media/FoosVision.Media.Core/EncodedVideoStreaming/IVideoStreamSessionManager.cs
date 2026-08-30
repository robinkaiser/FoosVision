// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.EncodedVideoStreaming;

public interface IVideoStreamSessionManager : IDisposable
{
    void Configure(string ipAddress, int port);

    void StartSession();

    void StopSession();

    void Enqueue(byte[] buffer, int offset, int length, long timeNs, bool markAsEndOfAccessUnit);
}
