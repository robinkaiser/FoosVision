// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Setup.StopSetup;

public record StopSetupRequest();

public interface IStopSetupInputPort
{
    Task Handle(StopSetupRequest request, IStopSetupOutputPort output, CancellationToken ct);
}
