// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Entities;
using FoosVision.UseCases.Game.Ports;

namespace FoosVision.Recorder.Composition.InMemoryStores;

internal class GameSessionStore : IGameSessionStore
{
    private readonly Lock _Gate = new();

    private Option<GameSession> _Active = Option<GameSession>.None();

    public bool HasActive
    {
        get { lock (_Gate) return _Active.IsSome; }
    }

    public Option<GameSession> LoadActive()
    {
        lock (_Gate) return _Active;
    }

    public void SaveActive(GameSession session)
    {
        lock (_Gate) _Active = session;
    }

    public void Clear()
    {
        lock (_Gate) _Active = Option<GameSession>.None();
    }
}
