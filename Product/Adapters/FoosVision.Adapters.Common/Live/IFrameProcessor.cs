// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Diagnostics.CodeAnalysis;
using FoosVision.Ports.Media;

namespace FoosVision.Adapters.Common.Live;

public interface IFrameProcessor
{
    /// <summary>
    /// Gets a value indicating whether a frame should be processed.
    /// </summary>
    bool ShouldProcess { get; }

    /// <summary>
    /// Process the next frame.
    ///
    /// IMPORTANT PROCESSING RULES:
    /// - This method is invoked sequentially; per-frame processing is not parallelized
    ///   across multiple frames.
    /// - Implementations MUST be fast enough that, on a single CPU core, processing of a
    ///   frame normally completes before the next frame arrives at the target frame rate.
    /// - Occasional overruns can be absorbed by the internal frame queue, but sustained
    ///   backlog or real-time violations are not acceptable.
    /// </summary>
    /// <param name="frame">Frame to be processed.</param>
    /// <param name="token">Cancellation token.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task Process([NotNull] IFrameHandle frame, CancellationToken token);
}
