// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Setup.Entities;
using FoosVision.UseCases.Setup.Ports;

namespace FoosVision.UseCases.Setup.CompleteTableUpdate;

public class CompleteTableUpdateInteractor : ICompleteTableUpdateInputPort
{
    private readonly ISetupSessionStore _SessionStore;

    public CompleteTableUpdateInteractor(ISetupSessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public Task Handle(CompleteTableUpdateRequest request, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out SetupSession session))
        {
            return Task.CompletedTask;
        }

        session.CompleteTableUpdate();

        return Task.CompletedTask;
    }
}
