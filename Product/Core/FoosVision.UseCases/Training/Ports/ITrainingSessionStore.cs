// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Training.Entities;

namespace FoosVision.UseCases.Training.Ports;

public interface ITrainingSessionStore
{
    bool HasActive { get; }
    Option<TrainingSession> LoadActive();
    void SaveActive(TrainingSession session);
    void Clear();
}
