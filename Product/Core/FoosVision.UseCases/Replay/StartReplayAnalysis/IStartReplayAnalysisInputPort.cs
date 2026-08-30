// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Replay.Entities;
using FoosVision.Domain.Replay.ValueObjects;
using FoosVision.Domain.Table.ValueObjects;

namespace FoosVision.UseCases.Replay.StartReplayAnalysis;

public record StartReplayAnalysisRequest(
    ReplayId ReplayId,
    ReplayTrackAnchor TrackAnchor,
    TableConfiguration TableConfiguration);

public interface IStartReplayAnalysisInputPort
{
    Task Handle(StartReplayAnalysisRequest request, IStartReplayAnalysisOutputPort output, CancellationToken ct);
}
