// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Installation.Entities;
using FoosVision.UseCases.Installation.Ports;

namespace FoosVision.Recorder.Composition.InMemoryStores;

internal class InstallSessionStore : IInstallSessionStore
{
    private readonly Lock _Gate = new();

    private Option<InstallSession> _Active = Option<InstallSession>.None();

    public bool HasActive
    {
        get { lock (_Gate) return _Active.IsSome; }
    }

    public Option<InstallSession> LoadActive()
    {
        lock (_Gate) return _Active;
    }

    public void SaveActive(InstallSession session)
    {
        lock (_Gate) _Active = session;
    }

    public void Clear()
    {
        lock (_Gate) _Active = Option<InstallSession>.None();
    }
}
