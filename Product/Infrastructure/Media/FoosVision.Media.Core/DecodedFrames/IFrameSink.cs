// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.DecodedFrames;

public interface IFrameSink
{
    /// <summary>
    /// Called by Device right before it writes a frame.
    /// </summary>
    /// <returns>Writable lease with a byte[] Buffer that can be filled.</returns>
    IProducerFrameHandle AcquireForWrite();
}
