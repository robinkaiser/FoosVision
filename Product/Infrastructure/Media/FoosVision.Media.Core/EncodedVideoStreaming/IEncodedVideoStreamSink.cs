// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.EncodedVideoStreaming;

public interface IEncodedVideoStreamSink : IDisposable
{
    /// <summary>
    /// Enables streaming to a remote UDP endpoint. Calling multiple times re-targets the stream.
    /// </summary>
    /// <param name="ipAddress">Endpoint IP address.</param>
    /// <param name="port">Endpoint port.</param>
    void Configure(string ipAddress, int port);

    /// <summary>
    /// Enqueue a raw Annex-B byte range for sending.
    /// The underlying buffer must remain valid until the sink sends it (bounded queue prevents long delays).
    /// </summary>
    /// <param name="buffer">NAL ring buffer memory.</param>
    /// <param name="offset">Offset of the byte range for sending.</param>
    /// <param name="length">Length of the byte range for sending.</param>
    /// <param name="timeNs">Presentation timestamp in nanoseconds.</param>
    /// <param name="markAsEndOfAccessUnit">Whether this range ends the current access unit.</param>
    void Enqueue(byte[] buffer, int offset, int length, long timeNs, bool markAsEndOfAccessUnit);
}
