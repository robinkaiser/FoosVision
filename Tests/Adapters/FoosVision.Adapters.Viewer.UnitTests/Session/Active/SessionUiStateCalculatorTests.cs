// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Protocol.Messages.Events;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class SessionUiStateCalculatorTests
{
    [Fact]
    public void CanToggle_disables_game_start_without_table_configuration()
    {
        bool canToggle = SessionUiStateCalculator.CanToggle(
            RecorderRuntimeMode.Idle,
            ActiveSessionPendingIntent.None,
            SessionMode.Game,
            isTableAvailable: false);

        Assert.False(canToggle);
    }

    [Fact]
    public void CanToggle_disables_toggles_while_command_is_pending()
    {
        bool canToggle = SessionUiStateCalculator.CanToggle(
            RecorderRuntimeMode.Idle,
            ActiveSessionPendingIntent.StartInstall,
            SessionMode.Install,
            isTableAvailable: true);

        Assert.False(canToggle);
    }

    [Fact]
    public void Calculate_uses_pending_intent_mode_when_recorder_is_idle()
    {
        SessionUiState state = SessionUiStateCalculator.Calculate(
            new SessionUiState(SessionMode.Install, false, true, false, false),
            RecorderRuntimeMode.Idle,
            ActiveSessionPendingIntent.StartGame,
            isTableAvailable: true,
            isReplayActive: false);

        Assert.Equal(SessionMode.Game, state.Mode);
        Assert.False(state.IsRunning);
        Assert.True(state.IsPendingCommand);
        Assert.True(state.IsGameAvailable);
    }

    [Fact]
    public void Calculate_publishes_replay_tracking_fps_when_replay_is_active()
    {
        SessionUiState state = SessionUiStateCalculator.Calculate(
            new SessionUiState(SessionMode.Game, true, true, false, false, TrackingFps: 42.3),
            RecorderRuntimeMode.GameRunning,
            ActiveSessionPendingIntent.None,
            isTableAvailable: true,
            isReplayActive: true);

        Assert.True(state.IsReplayActive);
        Assert.Equal(120.0, state.TrackingFps);
    }

    [Fact]
    public void UpdateTrackingFps_rounds_live_fps()
    {
        SessionUiState state = SessionUiStateCalculator.UpdateTrackingFps(
            new SessionUiState(SessionMode.Install, false, true, false, false),
            29.95,
            isReplayActive: false);

        Assert.Equal(30.0, state.TrackingFps);
    }
}
