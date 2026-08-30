// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Game.Entities;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.UseCases.Game.CompleteTableUpdate;

public class CompleteTableUpdateInteractor : ICompleteTableUpdateInputPort
{
    private readonly IGameSessionStore _SessionStore;

    public CompleteTableUpdateInteractor(IGameSessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public Task Handle(CompleteTableUpdateRequest request, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out GameSession session))
        {
            return Task.CompletedTask;
        }

        session.CompleteTableUpdate();

        return Task.CompletedTask;
    }
}
