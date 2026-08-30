// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Recorder.Connectivity;
using FoosVision.Adapters.Recorder.Diagnostics;
using FoosVision.Common.Metrics;
using FoosVision.Media.Core.Capture;
using FoosVision.Media.Core.EncodedVideoStreaming;
using FoosVision.Protocol.Connectivity.Configuration;
using FoosVision.Protocol.Messages.Events;
using FoosVision.Recorder.Composition.Diagnostics;
using FoosVision.Recorder.Composition.InMemoryStores;
using FoosVision.Recorder.Composition.Modules;
using FoosVision.Vision;

namespace FoosVision.Recorder.Composition;

internal class RecorderCompositionRoot : IDisposable
{
    private readonly SettingsStore _Settings;
    private readonly CameraController _CameraControl;
    private readonly VisionSession _Vision;
    private readonly InstallationModule _Installation;
    private readonly GameModule _Game;

    public NetworkModule Network { get; }

    public event Action? ViewerConnected;
    public event Action<RecorderRuntimeStateChanged>? RuntimeStateChanged;

    private RecorderCompositionRoot(
        SettingsStore settings,
        CameraController cameraControl,
        VisionSession vision,
        NetworkModule network,
        InstallationModule installation,
        GameModule game)
    {
        _Settings = settings;
        _CameraControl = cameraControl;
        _Vision = vision;
        _Installation = installation;
        _Game = game;
        Network = network;
    }

    public static RecorderCompositionRoot Compose(
        ICameraFeed cameraFeed,
        IRecorderVersionProvider versionProvider,
        IHandshakeDiagnosticsProvider diagnosticsProvider,
        IHandshakeViewerSettingsProvider viewerSettingsProvider,
        IVideoDumpWriter videoDumpWriter,
        bool publishObservations = false,
        bool publishBallDetectionMask = false,
        RuntimeMetricsOptions? runtimeMetricsOptions = null)
    {
        var settings = new SettingsStore();

        var nullUpdateTableScenePresenter = new NullUpdateTableScenePresenter();

        var encodedVideoStreamSinkFactory = new UdpRtpH264StreamSinkFactory(runtimeMetricsOptions);
        var streamSessionManager = new VideoStreamSessionManager(encodedVideoStreamSinkFactory);
        var cameraControl = new CameraController(cameraFeed, streamSessionManager);
        var backgroundQueue = new VideoDumpThreadPoolBackgroundQueue();
        var videoDumpOrchestrator = new VideoDumpOrchestrator(cameraControl, videoDumpWriter, backgroundQueue);

        // NOTE: CameraController currently uses this fixed layout internally (1920x1080 RGBA).
        var frameLayout = new VisionFrameLayout(
            Format: VisionPixelFormat.RGBA8888,
            Width: 1920,
            Height: 1080,
            Stride: 1920);
        var vision = new VisionSession(new VisionOptions(frameLayout));

        RecorderCompositionRoot? root = null;

        var network = new NetworkModule(
            versionProvider,
            diagnosticsProvider,
            viewerSettingsProvider,
            runtimeMetricsOptions,
            onHello: hello =>
            {
                cameraControl.ConfigureUdpVideoStream(hello.ViewerIpAddress, DefaultPorts.RtpH264StreamUdp);
                root?.ViewerConnected?.Invoke();
            });

        var runtimeState = new RecorderRuntimeStateController(network.EventPublisher);
        runtimeState.StateChanged += state => root?.RuntimeStateChanged?.Invoke(state);

        var installation = new InstallationModule(
            frameSource: cameraControl,
            frameFeed: cameraControl,
            tableConfigFinder: vision,
            tableConfigStore: settings,
            eventPublisher: network.EventPublisher,
            runtimeState: runtimeState,
            liveDataPublisher: network.LiveDataPublisher,
            videoDumpOrchestrator: videoDumpOrchestrator,
            runtimeMetricsOptions: runtimeMetricsOptions);

        var game = new GameModule(
            frameSource: cameraControl,
            frameFeed: cameraControl,
            replayBuffer: cameraControl,
            liveVideoStreamController: cameraControl,
            ballFinder: vision,
            ballDetectionMaskProvider: vision,
            tableConfigFinder: vision,
            tableSceneUpdater: vision,
            visionContextProvider: vision,
            settingsStore: settings,
            tableConfigStore: settings,
            eventPublisher: network.EventPublisher,
            runtimeState: runtimeState,
            liveDataPublisher: network.LiveDataPublisher,
            liveAnalysisPublisher: network.LiveAnalysisPublisher,
            updateTableScenePresenter: nullUpdateTableScenePresenter,
            videoDumpOrchestrator: videoDumpOrchestrator,
            publishObservations: publishObservations,
            publishBallDetectionMask: publishBallDetectionMask,
            runtimeMetricsOptions: runtimeMetricsOptions);

        var router = new RecorderCommandRouter(installation.CommandHandler, game.CommandHandler);
        network.SetCommandRouter(router);

        root = new RecorderCompositionRoot(settings, cameraControl, vision, network, installation, game);
        return root;
    }

    public async Task StopActiveSessions(CancellationToken ct)
    {
        await _Installation.StopIfActive(ct);
        await _Game.StopIfActive(ct);
        Network.ReleaseViewerConnection();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        // Stop network first (prevents late incoming commands triggering use-cases during teardown)
        Network.Dispose();
        _Game.Dispose();
        _Installation.Dispose();
    }
}
