// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Session;

public enum SessionMode
{
    Install,
    Game,
}

public record struct SessionUiState(
    SessionMode Mode,
    bool IsRunning,
    bool IsConnected,
    bool IsPendingCommand,
    bool IsFaulted,
    double? TrackingFps = null,
    bool IsReplayActive = false,
    bool IsGameAvailable = false);
