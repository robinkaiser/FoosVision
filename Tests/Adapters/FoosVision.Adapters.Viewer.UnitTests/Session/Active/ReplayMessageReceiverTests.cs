// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Domain.Replay.Entities;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class ReplayMessageReceiverTests
{
    [Fact]
    public void Early_return_is_retained_until_clip_and_finished_replay_rejects_duplicates()
    {
        Guid sessionId = Guid.NewGuid();
        ReplayId replayId = new(42, 1000);
        ReplayMessageReceiver sut = new();
        sut.SetSession(sessionId);
        Assert.True(sut.AcceptReturn(sessionId, replayId));
        Assert.False(sut.AcceptReturn(sessionId, replayId));
        Assert.True(sut.AcceptStarted(sessionId, replayId));
        Assert.False(sut.AcceptStarted(sessionId, replayId));
        Assert.True(sut.AcceptReplay(sessionId, replayId));
        Assert.True(sut.IsReturnRequested(replayId));
        Assert.False(sut.AcceptReplay(sessionId, replayId));
        sut.CompleteReplay(replayId);
        Assert.False(sut.AcceptStarted(sessionId, replayId));
        Assert.False(sut.AcceptReplay(sessionId, replayId));
        Assert.False(sut.AcceptReturn(sessionId, replayId));
    }

    [Fact]
    public void Old_messages_cannot_replace_new_replay_or_change_its_return_decision()
    {
        Guid sessionId = Guid.NewGuid();
        ReplayMessageReceiver sut = new();
        sut.SetSession(sessionId);
        ReplayId first = new(42, 1000);
        ReplayId next = new(84, 2000);
        sut.AcceptReturn(sessionId, first);
        Assert.True(sut.AcceptReplay(sessionId, next));
        Assert.False(sut.IsReturnRequested(next));
        Assert.False(sut.AcceptReturn(sessionId, first));
        Assert.False(sut.AcceptStarted(sessionId, first));
        Assert.False(sut.AcceptReplay(sessionId, first));
        Assert.False(sut.AcceptReturn(sessionId, new(85, 2000)));
        Assert.True(sut.AcceptReturn(sessionId, next));
        sut.CompleteReplay(first);
        Assert.True(sut.IsReturnRequested(next));
    }

    [Fact]
    public void Session_change_discards_old_messages_and_resets_recorder_timestamp_ordering()
    {
        Guid oldSession = Guid.NewGuid();
        Guid newSession = Guid.NewGuid();
        ReplayMessageReceiver sut = new();
        ReplayId oldReplay = new(42, 1000);
        Assert.False(sut.AcceptReplay(oldSession, oldReplay));
        sut.SetSession(oldSession);
        sut.AcceptReturn(oldSession, oldReplay);
        sut.SetSession(newSession);
        Assert.False(sut.AcceptReturn(oldSession, oldReplay));
        Assert.False(sut.AcceptReplay(oldSession, oldReplay));
        Assert.False(sut.AcceptStarted(oldSession, oldReplay));
        Assert.True(sut.AcceptReplay(newSession, new(1, 1)));
        Assert.False(sut.ReturnRequested);
        sut.SetSession(null);
        Assert.False(sut.AcceptReplay(newSession, new(2, 2)));
    }
}
