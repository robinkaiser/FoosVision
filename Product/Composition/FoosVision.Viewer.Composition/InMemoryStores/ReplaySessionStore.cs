// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.Viewer.Composition.InMemoryStores;

internal class ReplaySessionStore : IReplaySessionStore
{
    private readonly Lock _Gate = new();

    private Option<ReplaySession> _Active = Option<ReplaySession>.None();

    public bool HasActive
    {
        get { lock (_Gate) return _Active.IsSome; }
    }

    public Option<ReplaySession> LoadActive()
    {
        lock (_Gate) return _Active;
    }

    public void SaveActive(ReplaySession session)
    {
        lock (_Gate) _Active = session;
    }

    public void Clear()
    {
        lock (_Gate) _Active = Option<ReplaySession>.None();
    }
}
