// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Training.StopTraining;

public record StopTrainingRequest();

public interface IStopTrainingInputPort
{
    Task Handle(StopTrainingRequest request, IStopTrainingOutputPort output, CancellationToken ct);
}
