// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Game.StartGame;

public record StartGameResponse(Guid SessionId);

public interface IStartGameOutputPort
{
    Task ReportStarted(StartGameResponse response);

    Task ReportStartFailed(string reason);
}
