// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.Session.Playback;

namespace FoosVision.Viewer.App.Screen.Stage;

public interface IViewerScreenRuntime : IDisposable
{
    event Action<double?>? StreamFpsChanged;

    IOverlaySink OverlaySink { get; }

    IPlaybackController PlaybackController { get; }

    IPlaybackSourceFactory PlaybackSourceFactory { get; }

    void UpdateSessionUiState(SessionUiState state);

    void UpdateOverlayRotation(float rotationDegrees);
}
