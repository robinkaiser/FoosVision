// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Replay.ValueObjects;

namespace FoosVision.UseCases.Replay.CompleteReplayAnalysis;

public record ReplayAnalysisCompletedResponse(ReplayId ReplayId, ReplayAnalysis Analysis);

public interface ICompleteReplayAnalysisOutputPort
{
    Task ReportReplayAnalysisCompleted(ReplayAnalysisCompletedResponse response);

    Task ReportSkipped(string reason);
}
