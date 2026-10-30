// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.Entities;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.Training.CompleteTableSceneUpdate;

public class CompleteTableSceneUpdateInteractor : ICompleteTableSceneUpdateInputPort
{
    private readonly ITrainingSessionStore _Sessions;

    public CompleteTableSceneUpdateInteractor(ITrainingSessionStore sessions) => _Sessions = sessions;

    public Task Handle(CompleteTableSceneUpdateRequest request, CancellationToken ct)
    {
        if (_Sessions.LoadActive().TryGetValue(out TrainingSession session) && session.Id == request.SessionId)
        {
            session.CompleteTableSceneUpdate();
        }

        return Task.CompletedTask;
    }
}
