// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Domain.Training.ValueObjects;

public enum TrainingExecutionState
{
    Holding,
    Pending,
    Completed,
    Lost,
}

public record TrainingExecution(
    TrainingExecutionState State,
    Frame? MovementFrame = null,
    Frame? ConfirmationFrame = null,
    TrainingAnchor? Anchor = null);
