// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Game.Entities;

namespace FoosVision.UseCases.Game.Ports;

public interface IGameSessionStore
{
    bool HasActive { get; }

    Option<GameSession> LoadActive();

    void SaveActive(GameSession session);

    void Clear();
}
