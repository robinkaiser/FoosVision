// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Replay.CompleteReplayAnalysis;

public record CompleteReplayAnalysisRequest;

public interface ICompleteReplayAnalysisInputPort
{
    Task Handle(CompleteReplayAnalysisRequest request, ICompleteReplayAnalysisOutputPort output, CancellationToken ct);
}
