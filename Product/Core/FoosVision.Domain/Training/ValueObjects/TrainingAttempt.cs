// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.Training.ValueObjects;

public record TrainingAttempt(
    Guid SessionId,
    long AttemptId,
    TrainingStrategyKind Strategy,
    Team Team,
    BarType Bar,
    TrainingAnchor Anchor);
