// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Protocol.Connectivity.Abstractions;
using FoosVision.Protocol.Messages.Common;
using FoosVision.Protocol.Messages.Handshake;
using FoosVision.Protocol.Messages.Live;
using FoosVision.Protocol.Messages.LiveAnalysis;
using NSubstitute;
using NSubstitute.Core;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class SessionContext
{
    private readonly IRecorderLiveDataSubscriber _LiveDataSubscriber = Substitute.For<IRecorderLiveDataSubscriber>();
    private readonly IRecorderLiveAnalysisSubscriber _LiveAnalysisSubscriber = Substitute.For<IRecorderLiveAnalysisSubscriber>();
    private Action<TableUpdateMessage>? _TableUpdateHandler;
    private Action<TrackingFrameMessage>? _TrackingFrameHandler;
    private Action<VisionContextMessage>? _VisionContextHandler;
    private Action<BallDetectionMaskMessage>? _BallDetectionMaskHandler;
    private Action<ReplayStartedMessage>? _ReplayStartedHandler;
    private Action<ReplayMessage>? _ReplayHandler;

    public SessionContext()
    {
        OverlaySink = new RecordingOverlaySink(Events);
        PlaybackController = new RecordingPlaybackController(Events);
        Session.Connection.Returns(Connection);
        Session.LiveDataSubscriber.Returns(_LiveDataSubscriber);
        Session.LiveAnalysisSubscriber.Returns(_LiveAnalysisSubscriber);
        Session.StartInstallAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CreateAcceptedResponse);
        Session.StopInstallAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CreateAcceptedResponse);
        Session.StartGameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CreateAcceptedResponse);
        Session.StopGameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(CreateAcceptedResponse);

        _LiveDataSubscriber
            .Subscribe<TableUpdateMessage>(Arg.Any<Action<TableUpdateMessage>>())
            .Returns(callInfo =>
            {
                _TableUpdateHandler = callInfo.Arg<Action<TableUpdateMessage>>();
                return Substitute.For<IDisposable>();
            });

        _LiveDataSubscriber
            .Subscribe<TrackingFrameMessage>(Arg.Any<Action<TrackingFrameMessage>>())
            .Returns(callInfo =>
            {
                _TrackingFrameHandler = callInfo.Arg<Action<TrackingFrameMessage>>();
                return Substitute.For<IDisposable>();
            });

        _LiveAnalysisSubscriber
            .Subscribe<VisionContextMessage>(Arg.Any<Action<VisionContextMessage>>())
            .Returns(callInfo =>
            {
                _VisionContextHandler = callInfo.Arg<Action<VisionContextMessage>>();
                return Substitute.For<IDisposable>();
            });

        _LiveAnalysisSubscriber
            .Subscribe<BallDetectionMaskMessage>(Arg.Any<Action<BallDetectionMaskMessage>>())
            .Returns(callInfo =>
            {
                _BallDetectionMaskHandler = callInfo.Arg<Action<BallDetectionMaskMessage>>();
                return Substitute.For<IDisposable>();
            });

        _LiveAnalysisSubscriber
            .Subscribe<ReplayStartedMessage>(Arg.Any<Action<ReplayStartedMessage>>())
            .Returns(callInfo =>
            {
                _ReplayStartedHandler = callInfo.Arg<Action<ReplayStartedMessage>>();
                return Substitute.For<IDisposable>();
            });

        _LiveAnalysisSubscriber
            .Subscribe<ReplayMessage>(Arg.Any<Action<ReplayMessage>>())
            .Returns(callInfo =>
            {
                _ReplayHandler = callInfo.Arg<Action<ReplayMessage>>();
                return Substitute.For<IDisposable>();
            });
    }

    public RecorderConnection Connection { get; } = new(
        "192.168.178.10",
        "1.2.3-viewer",
        1,
        new HandshakeDiagnosticsSettings(),
        new HandshakeViewerSettings());

    public IConnectedViewerSession Session { get; } = Substitute.For<IConnectedViewerSession>();

    public RecordingUiSink UiSink { get; } = new();

    public List<string> Events { get; } = [];

    public RecordingOverlaySink OverlaySink { get; }

    public RecordingPlaybackController PlaybackController { get; }

    public RecordingVisionContextConsumer VisionContextConsumer { get; } = new();

    public RecordingBallFinder BallFinder { get; } = new();

    public RecordingBallDetectionMaskDecoder BallDetectionMaskDecoder { get; } = new();

    public RecordingReplayFrameDecoder ReplayFrameDecoder { get; } = new();

    public ActiveSession CreateSut()
    {
        return new ActiveSession(
            Session,
            UiSink,
            OverlaySink,
            new StubPlaybackSourceFactory(),
            PlaybackController,
            new RecordingReplaySessionStore(),
            VisionContextConsumer,
            BallFinder,
            BallDetectionMaskDecoder,
            ReplayFrameDecoder);
    }

    public void EnableReplayAnalysisPrerequisites()
    {
        PublishTableUpdate(TestMessages.CreateTableUpdateMessage());
        PublishVisionContext(new VisionContextMessage { Buffer = [1, 2, 3, 4] });
    }

    public void PublishTrackingFrame(TrackingFrameMessage message)
    {
        Assert.NotNull(_TrackingFrameHandler);
        _TrackingFrameHandler(message);
    }

    public void PublishTableUpdate(TableUpdateMessage message)
    {
        Assert.NotNull(_TableUpdateHandler);
        _TableUpdateHandler(message);
    }

    public void PublishReplay(ReplayMessage message)
    {
        Assert.NotNull(_ReplayHandler);
        _ReplayHandler(message);
    }

    public void PublishReplayStarted(ReplayStartedMessage message)
    {
        Assert.NotNull(_ReplayStartedHandler);
        _ReplayStartedHandler(message);
    }

    public void PublishVisionContext(VisionContextMessage message)
    {
        Assert.NotNull(_VisionContextHandler);
        _VisionContextHandler(message);
    }

    public void PublishBallDetectionMask(BallDetectionMaskMessage message)
    {
        Assert.NotNull(_BallDetectionMaskHandler);
        _BallDetectionMaskHandler(message);
    }

    public async Task WaitUntil(Func<bool> condition)
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

    private static CommandResponse CreateAcceptedResponse(CallInfo callInfo)
    {
        return new CommandResponse
        {
            Accepted = true,
            CommandId = callInfo.ArgAt<Guid>(0),
            Error = string.Empty,
        };
    }
}
