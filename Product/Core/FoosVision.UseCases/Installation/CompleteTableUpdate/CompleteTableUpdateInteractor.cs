// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.Installation.Entities;
using FoosVision.UseCases.Installation.Ports;

namespace FoosVision.UseCases.Installation.CompleteTableUpdate;

public class CompleteTableUpdateInteractor : ICompleteTableUpdateInputPort
{
    private readonly IInstallSessionStore _SessionStore;

    public CompleteTableUpdateInteractor(IInstallSessionStore sessionStore)
    {
        _SessionStore = sessionStore;
    }

    public Task Handle(CompleteTableUpdateRequest request, CancellationToken ct)
    {
        if (!_SessionStore.LoadActive().TryGetValue(out InstallSession session))
        {
            return Task.CompletedTask;
        }

        session.CompleteTableUpdate();

        return Task.CompletedTask;
    }
}
