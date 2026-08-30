// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Common.Types;
using FoosVision.Media.Android.Decoding;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.Viewer.Composition.Modules;
using FoosVision.Vision;

namespace FoosVision.Viewer.Composition;

internal class ViewerCompositionRoot : IDisposable
{
    private readonly NetworkModule _Network;
    private readonly VisionSession _Vision;
    private readonly IEncodedReplayFrameDecoder _ReplayFrameDecoder;
    private Option<ConnectedViewerSession> _ConnectedSession = Option<ConnectedViewerSession>.None();

    private ViewerCompositionRoot(NetworkModule network, VisionSession vision, IEncodedReplayFrameDecoder replayFrameDecoder)
    {
        _Network = network;
        _Vision = vision;
        _ReplayFrameDecoder = replayFrameDecoder;
    }

    public Option<ConnectedViewerSession> ConnectedSession => _ConnectedSession;

    public IEncodedVisionContextConsumer VisionContextConsumer => _Vision;

    public IBallFinder BallFinder => _Vision;

    public IEncodedBallDetectionMaskDecoder BallDetectionMaskDecoder => _Vision;

    public IEncodedReplayFrameDecoder ReplayFrameDecoder => _ReplayFrameDecoder;

    public static ViewerCompositionRoot Compose(
        RecorderConnectionOptions? connectionOptions,
        IRecorderFallbackCandidateSource? fallbackCandidateSource)
    {
        var network = new NetworkModule(connectionOptions, fallbackCandidateSource);

        // NOTE: Recorder and Viewer currently share the fixed FullHD RGBA vision layout.
        var frameLayout = new VisionFrameLayout(
            Format: VisionPixelFormat.RGBA8888,
            Width: 1920,
            Height: 1080,
            Stride: 1920);
        var vision = new VisionSession(new VisionOptions(frameLayout));

        var decoder = new AndroidEncodedReplayFrameDecoder();

        return new ViewerCompositionRoot(network, vision, decoder);
    }

    public async Task<RecorderConnectionResult> ConnectAsync(CancellationToken ct)
    {
        if (_ConnectedSession.TryGetValue(out var existing))
        {
            return RecorderConnectionResult.Connected(existing.Connection);
        }

        var result = await _Network.ConnectAsync(ct);

        if (!result.Success)
        {
            return result;
        }

        var installation = new InstallationModule(_Network.CommandClient);
        var game = new GameModule(_Network.CommandClient);
        var runtimeState = new RuntimeStateModule(_Network.EventSubscriber);

        _ConnectedSession = Option<ConnectedViewerSession>.Some(
            new ConnectedViewerSession(
                _Network.Connection,
                installation,
                game,
                runtimeState,
                _Network.LiveDataSubscriber,
                _Network.LiveAnalysisSubscriber,
                Disconnect));

        return result;
    }

    public void Disconnect()
    {
        if (_ConnectedSession.TryGetValue(out _))
        {
            _ConnectedSession = Option<ConnectedViewerSession>.None();
        }

        _Network.Disconnect();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        Disconnect();
        _Network.Dispose();
    }
}
