// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.Training.ValueObjects;

public enum TrainingAbortReason
{
    PrematureExecution,
    PreparationLost,
    ExecutionTimeout,
    BallLost,
}
