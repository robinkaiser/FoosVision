// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.UseCases.Training.Ports;

public interface ITrainingOptionsProvider
{
    TrainingOptions LoadTrainingOptions();
}
