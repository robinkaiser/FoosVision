// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.Session.Active;

internal class LiveTrackingPresenter
{
    private readonly IOverlaySink _OverlaySink;
    private readonly TrackingOverlayProjector _Projector;
    private readonly Func<bool> _IsReplayPending;
    private readonly Func<bool> _HasActiveReplay;
    private readonly Func<Point?, Task> _ObserveLiveTracking;

    public LiveTrackingPresenter(
        IOverlaySink overlaySink,
        TrackingOverlayProjector projector,
        Func<bool> isReplayPending,
        Func<bool> hasActiveReplay,
        Func<Point?, Task> observeLiveTracking)
    {
        _OverlaySink = overlaySink;
        _Projector = projector;
        _IsReplayPending = isReplayPending;
        _HasActiveReplay = hasActiveReplay;
        _ObserveLiveTracking = observeLiveTracking;
    }

    public void Handle(TrackingFrameMessage message)
    {
        if (_IsReplayPending())
        {
            return;
        }

        if (_HasActiveReplay())
        {
            _ = _ObserveLiveTracking(GetLiveBallPosition(message));
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

    private static Point? GetLiveBallPosition(TrackingFrameMessage message)
    {
        if (!message.IsBallFound ||
            message.BallPosition == null)
        {
            return null;
        }

        return new Point(message.BallPosition.X, message.BallPosition.Y);
    }
}
