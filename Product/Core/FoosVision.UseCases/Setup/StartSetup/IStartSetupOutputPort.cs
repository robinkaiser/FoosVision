// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Setup.StartSetup;

public record StartSetupResponse(Guid SessionId);

public interface IStartSetupOutputPort
{
    Task ReportStarted(StartSetupResponse response);

    Task ReportStartFailed(string reason);
}
