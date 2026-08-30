// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Installation.StartInstall;

public record StartInstallResponse(Guid SessionId);

public interface IStartInstallOutputPort
{
    Task ReportStarted(StartInstallResponse response);

    Task ReportStartFailed(string reason);
}
