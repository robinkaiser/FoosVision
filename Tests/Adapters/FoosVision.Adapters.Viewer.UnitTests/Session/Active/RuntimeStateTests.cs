// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Protocol.Messages.Events;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class RuntimeStateTests
{
    [Fact]
    public async Task OnRecorderRuntimeStateChanged_stops_playback_and_clears_tracking_when_game_stops()
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.GameRunning));
        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.Idle));
        await Task.Yield();

        Assert.Equal(2, context.OverlaySink.ClearTrackingStateCalls);
        Assert.Contains("stop-playback", context.Events);
        Assert.False(context.UiSink.States[^1].IsRunning);
    }
}
