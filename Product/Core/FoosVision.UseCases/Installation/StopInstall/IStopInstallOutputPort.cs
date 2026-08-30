// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Installation.StopInstall;

public record StopInstallResponse(Guid SessionId);

public interface IStopInstallOutputPort
{
    Task ReportStopped(StopInstallResponse response);

    Task ReportStopFailed(string reason);
}
