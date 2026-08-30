// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Ports.Media;

public interface IFrameHandle
{
    /// <summary>
    /// Gets the frame meta data including Id and timestamp.
    /// </summary>
    Frame Meta { get; }

    /// <summary>
    /// Gets the frame buffer.
    /// </summary>
    byte[] BufferRGBA8888 { get; }

    /// <summary>
    /// Gets the layout of the frame buffer.
    /// </summary>
    FrameLayout Layout { get; }

    /// <summary>
    /// Must be called to release the frame back to the pool.
    /// </summary>
    void Release();
}
