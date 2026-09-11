// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Setup.Entities;
using FoosVision.UseCases.Setup.Ports;

namespace FoosVision.Recorder.Composition.InMemoryStores;

internal class SetupSessionStore : ISetupSessionStore
{
    private readonly Lock _Gate = new();

    private Option<SetupSession> _Active = Option<SetupSession>.None();

    public bool HasActive
    {
        get { lock (_Gate) return _Active.IsSome; }
    }

    public Option<SetupSession> LoadActive()
    {
        lock (_Gate) return _Active;
    }

    public void SaveActive(SetupSession session)
    {
        lock (_Gate) _Active = session;
    }

    public void Clear()
    {
        lock (_Gate) _Active = Option<SetupSession>.None();
    }
}
