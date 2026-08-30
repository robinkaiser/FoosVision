// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Installation.StartInstall;

public record StartInstallRequest();

public interface IStartInstallInputPort
{
    Task Handle(StartInstallRequest request, IStartInstallOutputPort output, CancellationToken ct);
}
