// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.DecodedFrames;

public interface IProducerFrameHandle
{
    /// <summary>
    /// Gets writable pixel buffer.
    /// May be empty if out of buffers.
    /// </summary>
    byte[] BufferRGBA8888 { get; }

    /// <summary>
    /// Must be called by the device after filling Buffer.
    /// This seals the frame and notifies the pool that a new frame is ready.
    /// </summary>
    /// <param name="timestampNs">Frame timestamp in nanoseconds.</param>
    void MarkWritten(long timestampNs);
}
