// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Protocol.Messages.Handshake;
using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class ReplayCoordinatorContext : IDisposable
{
    private readonly ViewerPlaybackCoordinator _PlaybackCoordinator;
    private Option<TableConfiguration> _LatestTableConfiguration = Option<TableConfiguration>.None();
    private bool _HasVisionContext;

    public ReplayCoordinatorContext()
    {
        OverlaySink = new RecordingOverlaySink(Events);
        PlaybackController = new RecordingPlaybackController(Events);
        ReplaySessionStore = new RecordingReplaySessionStore();
        _PlaybackCoordinator = new ViewerPlaybackCoordinator(new StubPlaybackSourceFactory(), PlaybackController);
        Sut = new ViewerReplayCoordinator(
            OverlaySink,
            _PlaybackCoordinator,
            ReplaySessionStore,
            BallFinder,
            ReplayFrameDecoder,
            () => _LatestTableConfiguration,
            () => _HasVisionContext,
            ResetTrackingOverlay,
            StartLivePlaybackAsync,
            TrackingFpsUpdates.Add);
    }

    public RecorderConnection Connection { get; } = new(
        "192.168.178.10",
        "1.2.3-viewer",
        1,
        new HandshakeDiagnosticsSettings(),
        new HandshakeViewerSettings());

    public ViewerReplayCoordinator Sut { get; }

    public RecordingOverlaySink OverlaySink { get; }

    public RecordingPlaybackController PlaybackController { get; }

    public RecordingReplaySessionStore ReplaySessionStore { get; }

    public RecordingBallFinder BallFinder { get; } = new();

    public RecordingReplayFrameDecoder ReplayFrameDecoder { get; } = new();

    public List<string> Events { get; } = [];

    public List<double?> TrackingFpsUpdates { get; } = [];

    public void Dispose()
    {
        Sut.Dispose();
        _PlaybackCoordinator.Dispose();
    }

    public void EnableReplayAnalysisPrerequisites()
    {
        TableUpdateMessage tableUpdate = TestMessages.CreateTableUpdateMessage();
        Assert.True(TableConfigurationMessageMapper.TryMap(tableUpdate.TableConfiguration, out TableConfiguration tableConfiguration));
        _LatestTableConfiguration = Option<TableConfiguration>.Some(tableConfiguration);
        _HasVisionContext = true;
    }

    public static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 100; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }

        Assert.True(condition());
    }

    public void ApplyTableConfiguration()
    {
        TableUpdateMessage tableUpdate = TestMessages.CreateTableUpdateMessage();
        Assert.True(TableConfigurationMessageMapper.TryMap(tableUpdate.TableConfiguration, out TableConfiguration tableConfiguration));
        _LatestTableConfiguration = Option<TableConfiguration>.Some(tableConfiguration);
    }

    public void ApplyVisionContext()
    {
        _HasVisionContext = true;
    }

    private void ResetTrackingOverlay()
    {
        Sut.ResetAnalysis();
        OverlaySink.ClearTrackingState();
        OverlaySink.ClearBallDetectionMaskState();
    }

    private async Task StartLivePlaybackAsync()
    {
        ResetTrackingOverlay();
        await _PlaybackCoordinator.StartLiveAsync(Connection);
    }
}
