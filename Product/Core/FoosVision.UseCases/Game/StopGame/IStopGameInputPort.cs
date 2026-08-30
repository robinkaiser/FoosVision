// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Game.StopGame;

public record StopGameRequest();

public interface IStopGameInputPort
{
    Task Handle(StopGameRequest request, IStopGameOutputPort output, CancellationToken ct);
}
