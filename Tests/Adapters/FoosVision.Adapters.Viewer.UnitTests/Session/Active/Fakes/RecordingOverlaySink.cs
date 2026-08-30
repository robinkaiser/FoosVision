// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Overlays;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class RecordingOverlaySink : IOverlaySink
{
    private readonly List<string> _Events;

    public RecordingOverlaySink(List<string> events)
    {
        _Events = events;
    }

    public int ClearTrackingStateCalls { get; private set; }

    public int ClearBallDetectionMaskStateCalls { get; private set; }

    public List<TableOverlayState> TableStates { get; } = [];

    public List<TrackingOverlayState> TrackingStates { get; } = [];

    public List<BallDetectionMaskOverlayState> BallDetectionMaskStates { get; } = [];

    public void UpdateTrackingState(TrackingOverlayState state)
    {
        TrackingStates.Add(state);
    }

    public void ClearTrackingState()
    {
        ClearTrackingStateCalls++;
        _Events.Add("clear-tracking");
    }

    public void UpdateTableState(TableOverlayState state)
    {
        TableStates.Add(state);
    }

    public void UpdateBallDetectionMaskState(BallDetectionMaskOverlayState state)
    {
        BallDetectionMaskStates.Add(state);
    }

    public void ClearBallDetectionMaskState()
    {
        ClearBallDetectionMaskStateCalls++;
    }
}
