// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.Session.Active;

internal class LiveTrackingPresenter
{
    private readonly IOverlaySink _OverlaySink;
    private readonly TrackingOverlayProjector _Projector;
    private readonly Func<bool> _IsReplayPending;
    private readonly Func<bool> _HasActiveReplay;

    public LiveTrackingPresenter(
        IOverlaySink overlaySink,
        TrackingOverlayProjector projector,
        Func<bool> isReplayPending,
        Func<bool> hasActiveReplay)
    {
        _OverlaySink = overlaySink;
        _Projector = projector;
        _IsReplayPending = isReplayPending;
        _HasActiveReplay = hasActiveReplay;
    }

    public void Handle(TrackingFrameMessage message)
    {
        if (_IsReplayPending())
        {
            return;
        }

        if (_HasActiveReplay())
        {
            return;
        }

        TrackingOverlayState state = _Projector.Project(message);
        _OverlaySink.UpdateTrackingState(state);
    }

    public void UpdateTableConfiguration(TableConfiguration tableConfiguration)
    {
        _Projector.UpdateTableConfiguration(tableConfiguration);
    }

    public void ResetProjection()
    {
        _Projector.Reset();
    }

    public void Reset()
    {
        ResetProjection();
    }

}
