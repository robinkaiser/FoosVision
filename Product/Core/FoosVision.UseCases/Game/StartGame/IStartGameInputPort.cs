// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Game.StartGame;

public record StartGameRequest();

public interface IStartGameInputPort
{
    Task Handle(StartGameRequest request, IStartGameOutputPort output, CancellationToken ct);
}
