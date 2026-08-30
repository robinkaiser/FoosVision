// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;

namespace FoosVision.UseCases.Replay.StartReplayAnalysis;

public record ReplayAnalysisStartedResponse(ReplayId ReplayId);

public interface IStartReplayAnalysisOutputPort
{
    Task ReportReplayAnalysisStarted(ReplayAnalysisStartedResponse response);
}
