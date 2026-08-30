// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Entities;
using FoosVision.Domain.TrackingCore.Services.GameTracking;
using FoosVision.Domain.UnitTests;
using FoosVision.UseCases.Game.Ports;
using NSubstitute;

namespace FoosVision.UseCases.UnitTests.Game;

public class FakeGameSessionStore : IGameSessionStore
{
    private Option<GameSession> _GameSession = Option<GameSession>.None();

    public FakeGameSessionStore()
    {
        GameTracker = Substitute.For<IGameTracker>();

        RecreateSession();
    }

    // Convenience helpers

    public IGameTracker GameTracker { get; private set; }

    public void RecreateSession()
    {
        Guid guid = Guid.NewGuid();
        GameSession session = new(guid, GameTracker, TableConfig.Config);
        _GameSession = session;
    }

    // IGameSessionStore

    public bool HasActive => _GameSession.HasValue;

    public Option<GameSession> LoadActive()
    {
        return _GameSession;
    }

    public void SaveActive(GameSession session)
    {
        _GameSession = session;
    }

    public void Clear() => _GameSession = Option<GameSession>.None();
}
