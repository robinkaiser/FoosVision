// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class RecordingReplaySessionStore : IReplaySessionStore
{
    private Option<ReplaySession> _Session = Option<ReplaySession>.None();

    public bool HasActive => _Session.HasValue;

    public Option<ReplaySession> LoadActive()
    {
        return _Session;
    }

    public void SaveActive(ReplaySession session)
    {
        _Session = session;
    }

    public void Clear() => _Session = Option<ReplaySession>.None();
}
