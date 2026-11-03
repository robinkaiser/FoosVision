// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Protocol.Messages.Events;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class SessionUiStateCalculatorTests
{
    [Theory]
    [InlineData(RecorderRuntimeMode.Idle, SessionMode.Training, false, false)]
    [InlineData(RecorderRuntimeMode.Idle, SessionMode.Training, true, true)]
    [InlineData(RecorderRuntimeMode.SetupRunning, SessionMode.Training, true, false)]
    [InlineData(RecorderRuntimeMode.GameRunning, SessionMode.Training, true, false)]
    [InlineData(RecorderRuntimeMode.TrainingRunning, SessionMode.Training, false, true)]
    [InlineData(RecorderRuntimeMode.TrainingRunning, SessionMode.Game, true, false)]
    [InlineData(RecorderRuntimeMode.TrainingRunning, SessionMode.Setup, true, false)]
    [InlineData(RecorderRuntimeMode.Faulted, SessionMode.Training, true, false)]
    [InlineData(RecorderRuntimeMode.Idle, (SessionMode)99, true, false)]
    public void CanToggle_obeys_training_availability_and_exclusion(
        RecorderRuntimeMode runtimeMode, SessionMode mode, bool hasTable, bool expected)
    {
        Assert.Equal(expected, SessionUiStateCalculator.CanToggle(
            runtimeMode, ActiveSessionPendingIntent.None, mode, hasTable));
    }

    [Fact]
    public void Training_pending_intents_and_running_state_are_explicit()
    {
        Assert.Equal(ActiveSessionPendingIntent.StartTraining,
            SessionUiStateCalculator.GetPendingIntent(RecorderRuntimeMode.Idle, SessionMode.Training));
        Assert.Equal(ActiveSessionPendingIntent.StopTraining,
            SessionUiStateCalculator.GetPendingIntent(RecorderRuntimeMode.TrainingRunning, SessionMode.Training));
        Assert.Equal(ActiveSessionPendingIntent.None,
            SessionUiStateCalculator.GetPendingIntent(RecorderRuntimeMode.Idle, (SessionMode)99));

        SessionUiState current = new(SessionMode.Setup, false, true, false, false, ProcessFps: 29.8);
        SessionUiState pending = SessionUiStateCalculator.Calculate(
            current, RecorderRuntimeMode.Idle, ActiveSessionPendingIntent.StartTraining, true, false);
        Assert.Equal(SessionMode.Training, pending.Mode);
        Assert.True(pending.IsPendingCommand);
        Assert.True(pending.IsTrainingAvailable);

        SessionUiState running = SessionUiStateCalculator.Calculate(
            current, RecorderRuntimeMode.TrainingRunning, ActiveSessionPendingIntent.None, true, true);
        Assert.Equal(SessionMode.Training, running.Mode);
        Assert.True(running.IsRunning);
        Assert.True(running.IsReplayActive);
        Assert.Equal(29.8, running.ProcessFps);
    }

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
            ActiveSessionPendingIntent.StartSetup,
            SessionMode.Setup,
            isTableAvailable: true);

        Assert.False(canToggle);
    }

    [Fact]
    public void Calculate_uses_pending_intent_mode_when_recorder_is_idle()
    {
        SessionUiState state = SessionUiStateCalculator.Calculate(
            new SessionUiState(SessionMode.Setup, false, true, false, false),
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
    public void Calculate_preserves_process_fps_when_replay_is_active()
    {
        SessionUiState state = SessionUiStateCalculator.Calculate(
            new SessionUiState(SessionMode.Game, true, true, false, false, ProcessFps: 42.3),
            RecorderRuntimeMode.GameRunning,
            ActiveSessionPendingIntent.None,
            isTableAvailable: true,
            isReplayActive: true);

        Assert.True(state.IsReplayActive);
        Assert.Equal(42.3, state.ProcessFps);
    }

    [Fact]
    public void Calculate_clears_process_fps_when_recorder_is_not_running()
    {
        SessionUiState state = SessionUiStateCalculator.Calculate(
            new SessionUiState(SessionMode.Game, true, true, false, false, ProcessFps: 42.3),
            RecorderRuntimeMode.Idle,
            ActiveSessionPendingIntent.None,
            isTableAvailable: true,
            isReplayActive: false);

        Assert.False(state.IsRunning);
        Assert.Null(state.ProcessFps);
    }

    [Fact]
    public void UpdateProcessFps_rounds_live_fps()
    {
        SessionUiState state = SessionUiStateCalculator.UpdateProcessFps(
            new SessionUiState(SessionMode.Setup, false, true, false, false),
            29.95,
            isReplayActive: false);

        Assert.Equal(30.0, state.ProcessFps);
    }
}
