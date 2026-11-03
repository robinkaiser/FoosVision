// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Protocol.Messages.Events;
using static FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes.TestMessages;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class RuntimeStateTests
{
    [Theory]
    [InlineData(RecorderRuntimeMode.GameRunning)]
    [InlineData(RecorderRuntimeMode.TrainingRunning)]
    public async Task OnRecorderRuntimeStateChanged_stops_playback_and_clears_tracking_when_session_stops(RecorderRuntimeMode mode)
    {
        SessionContext context = new();
        using ActiveSession sut = context.CreateSut();

        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(mode));
        sut.OnRecorderRuntimeStateChanged(CreateRuntimeState(RecorderRuntimeMode.Idle));
        await Task.Yield();

        Assert.Equal(2, context.OverlaySink.ClearTrackingStateCalls);
        Assert.Contains("stop-playback", context.Events);
        Assert.False(context.UiSink.States[^1].IsRunning);
    }
}
