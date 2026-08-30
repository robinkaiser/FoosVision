// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Common.Metrics;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.Session.Active;

internal class LiveTrackingPresenter
{
    private static readonly TimeSpan _TrackingFpsWindow = TimeSpan.FromSeconds(3);

    private readonly IOverlaySink _OverlaySink;
    private readonly TrackingOverlayProjector _Projector;
    private readonly Func<DateTimeOffset> _UtcNow;
    private readonly Func<bool> _IsReplayPending;
    private readonly Func<bool> _HasActiveReplay;
    private readonly Func<Point?, Task> _ObserveLiveTracking;
    private readonly Lock _TrackingFpsSync = new();
    private readonly SlidingFrameRateCounter _TrackingFrameRateCounter = new(_TrackingFpsWindow);

    public LiveTrackingPresenter(
        IOverlaySink overlaySink,
        TrackingOverlayProjector projector,
        Func<DateTimeOffset> utcNow,
        Func<bool> isReplayPending,
        Func<bool> hasActiveReplay,
        Func<Point?, Task> observeLiveTracking)
    {
        _OverlaySink = overlaySink;
        _Projector = projector;
        _UtcNow = utcNow;
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

        lock (_TrackingFpsSync)
        {
            _TrackingFrameRateCounter.Record(_UtcNow());
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

        lock (_TrackingFpsSync)
        {
            _TrackingFrameRateCounter.Reset();
        }
    }

    public double? GetFramesPerSecond()
    {
        lock (_TrackingFpsSync)
        {
            return _TrackingFrameRateCounter.GetFramesPerSecond(_UtcNow());
        }
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
