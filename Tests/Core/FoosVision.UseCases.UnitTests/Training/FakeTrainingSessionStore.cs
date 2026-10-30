// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Training.Entities;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.UnitTests.Training;

public class FakeTrainingSessionStore : ITrainingSessionStore
{
    private Option<TrainingSession> _Session = Option<TrainingSession>.None();
    public bool HasActive => _Session.HasValue;
    public Option<TrainingSession> LoadActive() => _Session;
    public void SaveActive(TrainingSession session) => _Session = session;
    public void Clear() => _Session = Option<TrainingSession>.None();
}
