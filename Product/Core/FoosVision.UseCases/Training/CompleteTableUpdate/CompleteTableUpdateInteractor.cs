// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Training.Entities;
using FoosVision.UseCases.Training.Ports;

namespace FoosVision.UseCases.Training.CompleteTableUpdate;

public class CompleteTableUpdateInteractor : ICompleteTableUpdateInputPort
{
    private readonly ITrainingSessionStore _Sessions;

    public CompleteTableUpdateInteractor(ITrainingSessionStore sessions) => _Sessions = sessions;

    public Task Handle(CompleteTableUpdateRequest request, CancellationToken ct)
    {
        if (_Sessions.LoadActive().TryGetValue(out TrainingSession session) && session.Id == request.SessionId)
        {
            session.CompleteTableUpdate();
        }

        return Task.CompletedTask;
    }
}
