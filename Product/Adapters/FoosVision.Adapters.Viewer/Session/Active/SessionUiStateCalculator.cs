// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Events;

namespace FoosVision.Adapters.Viewer.Session.Active;

internal enum ActiveSessionPendingIntent
{
    None = 0,
    StartSetup = 1,
    StopSetup = 2,
    StartGame = 3,
    StopGame = 4,
}

internal static class SessionUiStateCalculator
{
    public static bool CanToggle(
        RecorderRuntimeMode runtimeMode,
        ActiveSessionPendingIntent pendingIntent,
        SessionMode requestedMode,
        bool isTableAvailable)
    {
        if (pendingIntent != ActiveSessionPendingIntent.None)
        {
            return false;
        }

        return runtimeMode switch
        {
            RecorderRuntimeMode.Idle => CanStartMode(requestedMode, isTableAvailable),
            RecorderRuntimeMode.SetupRunning => requestedMode == SessionMode.Setup,
            RecorderRuntimeMode.GameRunning => requestedMode == SessionMode.Game,
            RecorderRuntimeMode.Faulted => false,
            _ => false,
        };
    }

    public static ActiveSessionPendingIntent GetPendingIntent(
        RecorderRuntimeMode runtimeMode,
        SessionMode requestedMode)
    {
        if (requestedMode == SessionMode.Setup)
        {
            return runtimeMode == RecorderRuntimeMode.SetupRunning
                ? ActiveSessionPendingIntent.StopSetup
                : ActiveSessionPendingIntent.StartSetup;
        }

        return runtimeMode == RecorderRuntimeMode.GameRunning
            ? ActiveSessionPendingIntent.StopGame
            : ActiveSessionPendingIntent.StartGame;
    }

    public static SessionUiState Calculate(
        SessionUiState currentState,
        RecorderRuntimeMode runtimeMode,
        ActiveSessionPendingIntent pendingIntent,
        bool isTableAvailable,
        bool isReplayActive)
    {
        SessionMode mode = runtimeMode switch
        {
            RecorderRuntimeMode.SetupRunning => SessionMode.Setup,
            RecorderRuntimeMode.GameRunning => SessionMode.Game,
            _ => pendingIntent switch
            {
                ActiveSessionPendingIntent.StartSetup => SessionMode.Setup,
                ActiveSessionPendingIntent.StopSetup => SessionMode.Setup,
                ActiveSessionPendingIntent.StartGame => SessionMode.Game,
                ActiveSessionPendingIntent.StopGame => SessionMode.Game,
                _ => currentState.Mode,
            },
        };

        return new SessionUiState(
            Mode: mode,
            IsRunning: runtimeMode is RecorderRuntimeMode.SetupRunning or RecorderRuntimeMode.GameRunning,
            IsConnected: true,
            IsPendingCommand: pendingIntent != ActiveSessionPendingIntent.None,
            IsFaulted: runtimeMode == RecorderRuntimeMode.Faulted,
            ProcessFps: runtimeMode is RecorderRuntimeMode.SetupRunning or RecorderRuntimeMode.GameRunning
                ? currentState.ProcessFps
                : null,
            IsReplayActive: isReplayActive,
            IsGameAvailable: isTableAvailable);
    }

    public static SessionUiState UpdateProcessFps(
        SessionUiState currentState,
        double? processFps,
        bool isReplayActive)
    {
        double? roundedProcessFps = processFps.HasValue
            ? Math.Round(processFps.Value, 1, MidpointRounding.AwayFromZero)
            : null;

        return currentState with
        {
            ProcessFps = roundedProcessFps,
            IsReplayActive = isReplayActive,
        };
    }

    private static bool CanStartMode(SessionMode mode, bool isTableAvailable)
    {
        return mode switch
        {
            SessionMode.Setup => true,
            SessionMode.Game => isTableAvailable,
            _ => false,
        };
    }
}
