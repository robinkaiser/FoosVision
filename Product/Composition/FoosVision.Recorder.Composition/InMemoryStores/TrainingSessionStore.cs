// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Training.Entities;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.Recorder.Composition.InMemoryStores;

internal class TrainingSessionStore : ITrainingSessionStore
{
    private readonly Lock _Gate = new();

    private Option<TrainingSession> _Active = Option<TrainingSession>.None();

    public bool HasActive
    {
        get { lock (_Gate) return _Active.IsSome; }
    }

    public Option<TrainingSession> LoadActive()
    {
        lock (_Gate) return _Active;
    }

    public void SaveActive(TrainingSession session)
    {
        lock (_Gate) _Active = session;
    }

    public void Clear()
    {
        lock (_Gate) _Active = Option<TrainingSession>.None();
    }
}
