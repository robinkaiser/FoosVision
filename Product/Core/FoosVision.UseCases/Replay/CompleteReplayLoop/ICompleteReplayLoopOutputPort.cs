// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.UseCases.Replay.RequestReturnToLive;

namespace FoosVision.UseCases.Replay.CompleteReplayLoop;

public interface ICompleteReplayLoopOutputPort
{
    Task ReportReturnToLive(ReturnToLiveResponse response);

    Task ReportSkipped(string reason);
}
