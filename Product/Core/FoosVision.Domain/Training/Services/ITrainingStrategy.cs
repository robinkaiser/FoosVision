// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.Services.LiveTracking;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Domain.Training.ValueObjects;

namespace FoosVision.Domain.Training.Services;

public record TrainingPreparation(Team Team, BarType Bar, TrainingAnchor Anchor);

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

public interface ITrainingStrategy
{
    TrainingStrategyOptions Options { get; }
    string ReadyText { get; }
    IReadOnlyList<TrainingVariant> Variants { get; }
    TrainingPreparation? FindPreparation(LiveTrackingSnapshot snapshot);
    void Begin(TrainingPreparation preparation);
    TrainingExecution Evaluate(LiveTrackingSnapshot snapshot);
    void UpdateTableConfig(TableConfiguration tableConfig);
    void Reset(bool requireNewPreparation = false);
}
