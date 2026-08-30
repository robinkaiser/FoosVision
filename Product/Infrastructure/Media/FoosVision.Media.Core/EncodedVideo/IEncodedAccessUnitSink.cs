// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Core.EncodedVideo;

/// <summary>
/// A sink for writing encoded units into.
/// </summary>
public interface IEncodedAccessUnitSink
{
    /// <summary>
    /// Gets the ring buffer memory to write into.
    /// </summary>
    byte[] Buffer { get; }

    /// <summary>
    /// Gets the offset where to start writing the next unit into.
    /// </summary>
    int Offset { get; }

    /// <summary>
    /// Once device wrote [size] bytes starting at Offset, it notifies.
    /// </summary>
    /// <param name="timestampNs">Timestamp of the unit written.</param>
    /// <param name="size">Number of bytes written to the buffer.</param>
    void Completed(long timestampNs, int size);
}
