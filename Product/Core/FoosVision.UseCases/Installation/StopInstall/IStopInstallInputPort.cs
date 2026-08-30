// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Installation.StopInstall;

public record StopInstallRequest();

public interface IStopInstallInputPort
{
    Task Handle(StopInstallRequest request, IStopInstallOutputPort output, CancellationToken ct);
}
