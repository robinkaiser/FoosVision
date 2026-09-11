// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Setup.StartSetup;

public record StartSetupRequest();

public interface IStartSetupInputPort
{
    Task Handle(StartSetupRequest request, IStartSetupOutputPort output, CancellationToken ct);
}
