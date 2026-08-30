// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;

namespace FoosVision.UseCases.Replay.ProcessReplayFrame;

public record ReplayFrameProcessedResponse(ReplayId ReplayId);

public interface IProcessReplayFrameOutputPort
{
    Task ReportReplayFrameProcessed(ReplayFrameProcessedResponse response);

    Task ReportSkipped(string reason);
}
