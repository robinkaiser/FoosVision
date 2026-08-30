// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Session.Overlays;

public interface IOverlaySink
{
    void UpdateTrackingState(TrackingOverlayState state);

    void ClearTrackingState();

    void UpdateTableState(TableOverlayState state);

    void UpdateBallDetectionMaskState(BallDetectionMaskOverlayState state);

    void ClearBallDetectionMaskState();
}
