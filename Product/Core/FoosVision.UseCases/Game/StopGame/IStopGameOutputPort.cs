// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Game.StopGame;

public record StopGameResponse(Guid SessionId);

public interface IStopGameOutputPort
{
    Task ReportStopped(StopGameResponse response);

    Task ReportStopFailed(string reason);
}
