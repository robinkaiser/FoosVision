// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.UseCases.Training.StartTraining;

public record StartTrainingRequest();

public interface IStartTrainingInputPort
{
    Task Handle(StartTrainingRequest request, IStartTrainingOutputPort output, CancellationToken ct);
}
