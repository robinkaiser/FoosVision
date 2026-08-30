// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.UseCases.UnitTests.Replay;

public class FakeReplaySessionStore : IReplaySessionStore
{
    private Option<ReplaySession> _ReplaySession = Option<ReplaySession>.None();

    public bool HasActive => _ReplaySession.HasValue;

    public Option<ReplaySession> LoadActive()
    {
        return _ReplaySession;
    }

    public void SaveActive(ReplaySession session)
    {
        _ReplaySession = session;
    }

    public void Clear() => _ReplaySession = Option<ReplaySession>.None();
}
