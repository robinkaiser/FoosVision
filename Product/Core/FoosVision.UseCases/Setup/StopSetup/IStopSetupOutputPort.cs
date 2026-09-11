// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Setup.StopSetup;

public record StopSetupResponse(Guid SessionId);

public interface IStopSetupOutputPort
{
    Task ReportStopped(StopSetupResponse response);

    Task ReportStopFailed(string reason);
}
