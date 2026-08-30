// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Replay.CompleteReplayLoop;

public interface ICompleteReplayLoopOutputPort
{
    Task ReportSkipped(string reason);
}
