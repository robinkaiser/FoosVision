// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;

namespace FoosVision.UseCases.Replay.StopReplay;

public record ReplayStoppedResponse(ReplayId ReplayId);

public interface IStopReplayOutputPort
{
    Task ReportStopped(ReplayStoppedResponse response);

    Task ReportStopFailed(string reason);
}
