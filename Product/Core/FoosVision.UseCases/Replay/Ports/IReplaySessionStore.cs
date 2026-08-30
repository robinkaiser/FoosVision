// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;

namespace FoosVision.UseCases.Replay.Ports;

public interface IReplaySessionStore
{
    bool HasActive { get; }

    Option<ReplaySession> LoadActive();

    void SaveActive(ReplaySession session);

    void Clear();
}
