// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Dependencies.Video;

public enum FrameSourceResult
{
    Success,
    Failure,
}

public interface IFrameSource
{
    Task<FrameSourceResult> Configure(CancellationToken ct);

    Task<FrameSourceResult> Start(CancellationToken ct);

    Task Stop(CancellationToken ct);
}
