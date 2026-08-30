// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;

namespace FoosVision.Ports.Media;

public interface IFrameFeed
{
    /// <summary>
    /// Raised whenever a new frame is captured and ready for processing.
    ///
    /// IMPORTANT LIFETIME / THREADING RULES:
    /// - This callback is invoked on the camera/producer thread. The handler
    ///   MUST return quickly and MUST NOT do heavy work (vision, interactor calls,
    ///   etc.) inline. Instead, enqueue/defer the work to your own worker thread
    ///   or task.
    ///
    /// - The provided IFrameHandle represents a pooled frame buffer. The consumer
    ///   becomes responsible for eventually calling frameHandle.Release() exactly
    ///   once when the frame is no longer needed.
    ///
    /// - If the frame is not needed (e.g. no active session), the handler should
    ///   call frameHandle.Release() immediately.
    /// </summary>
    event Action<IFrameHandle> FrameReady;

    /// <summary>
    /// Try aquire a frame based on given id.
    ///
    /// IMPORTANT LIFETIME / THREADING RULES:
    /// - The provided IFrameHandle represents a pooled frame buffer. The consumer
    ///   becomes responsible for eventually calling frameHandle.Release() exactly
    ///   once when the frame is no longer needed.
    /// </summary>
    /// <param name="id">Frame Id.</param>
    /// <param name="handle">Output frame handle.</param>
    /// <returns>True if aquired successfully.</returns>
    bool TryAcquireById(ulong id, [NotNullWhen(true)] out IFrameHandle? handle);
}
