// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.Session.Playback;
using FoosVision.Common.Types;
using FoosVision.Domain.Replay.Entities;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Handshake;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using FoosVision.UseCases.Replay.Ports;
using NSubstitute;

namespace FoosVision.Adapters.Viewer.UnitTests.Session;

public class SessionManagerTests
{
    [Fact]
    public async Task InitializeAsync_updates_ui_state_when_connection_succeeds()
    {
        TestContext context = new();
        using SessionManager sut = context.CreateSut(connection => context.ConnectedCallbackConnection = connection);

        await sut.InitializeAsync(CancellationToken.None);

        Assert.True(context.UiSink.States[^1].IsConnected);
        Assert.False(context.UiSink.States[^1].IsFaulted);
        context.Session.Received(1).AttachRuntimeStateSink(Arg.Any<IRecorderRuntimeStateSink>());
    }

    [Fact]
    public async Task InitializeAsync_keeps_ui_disconnected_when_connection_fails()
    {
        TestContext context = new();
        context.Host.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(RecorderConnectionResult.Failed(RecorderConnectionFailure.NoCandidateFound));
        using SessionManager sut = context.CreateSut(connection => context.ConnectedCallbackConnection = connection);

        await sut.InitializeAsync(CancellationToken.None);

        Assert.False(context.UiSink.States[^1].IsConnected);
        Assert.False(context.UiSink.States[^1].IsPendingCommand);
    }

    [Fact]
    public async Task InitializeAsync_stops_retrying_when_cancelled()
    {
        TestContext context = new();
        using CancellationTokenSource cts = new();
        context.Host.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                return RecorderConnectionResult.Failed(RecorderConnectionFailure.Cancelled);
            });
        using SessionManager sut = context.CreateSut(connection => context.ConnectedCallbackConnection = connection);

        await sut.InitializeAsync(cts.Token);

        await context.Host.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
        Assert.False(context.UiSink.States[^1].IsConnected);
    }

    [Fact]
    public async Task InitializeAsync_invokes_connected_callback_when_connection_succeeds()
    {
        TestContext context = new();
        using SessionManager sut = context.CreateSut(connection => context.ConnectedCallbackConnection = connection);

        await sut.InitializeAsync(CancellationToken.None);

        Assert.Same(context.Connection, context.ConnectedCallbackConnection);
    }

    private sealed class TestContext
    {
        private readonly IRecorderLiveDataSubscriber _LiveDataSubscriber = Substitute.For<IRecorderLiveDataSubscriber>();
        private readonly IRecorderLiveAnalysisSubscriber _LiveAnalysisSubscriber = Substitute.For<IRecorderLiveAnalysisSubscriber>();

        public TestContext()
        {
            Session.Connection.Returns(Connection);
            Session.LiveDataSubscriber.Returns(_LiveDataSubscriber);
            Session.LiveAnalysisSubscriber.Returns(_LiveAnalysisSubscriber);
            Host.ConnectedSession.Returns(Option<IConnectedViewerSession>.Some(Session));
            Host.ReplaySessionStore.Returns(ReplaySessionStore);
            Host.VisionContextConsumer.Returns(VisionContextConsumer);
            Host.BallFinder.Returns(BallFinder);
            Host.BallDetectionMaskDecoder.Returns(BallDetectionMaskDecoder);
            Host.ReplayFrameDecoder.Returns(ReplayFrameDecoder);
            Host.ConnectAsync(Arg.Any<CancellationToken>())
                .Returns(RecorderConnectionResult.Connected(Connection));

            _LiveDataSubscriber
                .Subscribe<TableUpdateMessage>(Arg.Any<Action<TableUpdateMessage>>())
                .Returns(Substitute.For<IDisposable>());

            _LiveDataSubscriber
                .Subscribe<TrackingFrameMessage>(Arg.Any<Action<TrackingFrameMessage>>())
                .Returns(Substitute.For<IDisposable>());

            _LiveAnalysisSubscriber
                .Subscribe<VisionContextMessage>(Arg.Any<Action<VisionContextMessage>>())
                .Returns(Substitute.For<IDisposable>());

            _LiveAnalysisSubscriber
                .Subscribe<BallDetectionMaskMessage>(Arg.Any<Action<BallDetectionMaskMessage>>())
                .Returns(Substitute.For<IDisposable>());

            _LiveAnalysisSubscriber
                .Subscribe<ReplayStartedMessage>(Arg.Any<Action<ReplayStartedMessage>>())
                .Returns(Substitute.For<IDisposable>());

            _LiveAnalysisSubscriber
                .Subscribe<ReplayMessage>(Arg.Any<Action<ReplayMessage>>())
                .Returns(Substitute.For<IDisposable>());
        }

        public RecorderConnection Connection { get; } = new(
            "192.168.178.10",
            "1.2.3-viewer",
            1,
            new HandshakeDiagnosticsSettings(),
            new HandshakeViewerSettings());

        public RecorderConnection? ConnectedCallbackConnection { get; set; }

        public IViewerSessionHost Host { get; } = Substitute.For<IViewerSessionHost>();

        public IConnectedViewerSession Session { get; } = Substitute.For<IConnectedViewerSession>();

        public IReplaySessionStore ReplaySessionStore { get; } = new RecordingReplaySessionStore();

        public IEncodedVisionContextConsumer VisionContextConsumer { get; } = Substitute.For<IEncodedVisionContextConsumer>();

        public IBallFinder BallFinder { get; } = Substitute.For<IBallFinder>();

        public IEncodedBallDetectionMaskDecoder BallDetectionMaskDecoder { get; } = Substitute.For<IEncodedBallDetectionMaskDecoder>();

        public IEncodedReplayFrameDecoder ReplayFrameDecoder { get; } = Substitute.For<IEncodedReplayFrameDecoder>();

        public RecordingUiSink UiSink { get; } = new();

        public RecordingOverlaySink OverlaySink { get; } = new();

        public SessionManager CreateSut(Action<RecorderConnection> onConnected)
        {
            return new SessionManager(
                UiSink,
                OverlaySink,
                new StubPlaybackSourceFactory(),
                new NoOpPlaybackController(),
                Host,
                onConnected);
        }
    }

    private sealed class RecordingReplaySessionStore : IReplaySessionStore
    {
        private Option<ReplaySession> _Session = Option<ReplaySession>.None();

        public bool HasActive => _Session.HasValue;

        public Option<ReplaySession> LoadActive()
        {
            return _Session;
        }

        public void SaveActive(ReplaySession session)
        {
            _Session = session;
        }

        public void Clear() => _Session = Option<ReplaySession>.None();
    }

    private sealed class RecordingUiSink : IUiStateSink
    {
        public List<SessionUiState> States { get; } = [];

        public void Update(SessionUiState uiState)
        {
            States.Add(uiState);
        }
    }

    private sealed class RecordingOverlaySink : IOverlaySink
    {
        public void UpdateTrackingState(TrackingOverlayState state)
        {
        }

        public void ClearTrackingState()
        {
        }

        public void UpdateTableState(TableOverlayState state)
        {
        }

        public void UpdateBallDetectionMaskState(BallDetectionMaskOverlayState state)
        {
        }

        public void ClearBallDetectionMaskState()
        {
        }
    }

    private sealed class StubPlaybackSourceFactory : IPlaybackSourceFactory
    {
        public PlaybackRequest CreateStreamSource(RecorderConnection connection)
        {
            return new PlaybackRequest("cache://stream.sdp", PlaybackKind.LiveStream);
        }
    }

    private sealed class NoOpPlaybackController : IPlaybackController
    {
        public event Func<Task>? ReplayLoopCompleted;

        public event Func<long, Task>? ReplayPositionChanged;

        public Task StartAsync(PlaybackRequest playbackRequest)
        {
            _ = ReplayLoopCompleted;
            _ = ReplayPositionChanged;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }
    }
}
