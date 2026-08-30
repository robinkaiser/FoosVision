// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Game.Entities;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.UseCases.Game.CompleteTableSceneUpdate;

public class CompleteTableSceneUpdateInteractor : ICompleteTableSceneUpdateInputPort
{
    private readonly IGameSessionStore _SessionStore;

    public CompleteTableSceneUpdateInteractor(IGameSessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public Task Handle(CompleteTableSceneUpdateRequest request, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out GameSession session))
        {
            return Task.CompletedTask;
        }

        session.CompleteTableSceneUpdate();

        return Task.CompletedTask;
    }
}
