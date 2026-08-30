// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Adapters.Viewer.Session;
using FoosVision.Common.Types;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Replay.Ports;
using FoosVision.Viewer.Composition.InMemoryStores;
using NetMQ;

namespace FoosVision.Viewer.Composition;

public class ViewerHost :
    IViewerSessionHost,
    IDisposable
{
    private readonly ViewerCompositionRoot _Root;
    private readonly ReplaySessionStore _ReplaySessionStore = new();

    public ViewerHost(
        RecorderConnectionOptions? connectionOptions = null,
        IRecorderFallbackCandidateSource? fallbackCandidateSource = null)
    {
        _Root = ViewerCompositionRoot.Compose(connectionOptions, fallbackCandidateSource);
    }

    public Option<IConnectedViewerSession> ConnectedSession => _Root.ConnectedSession.Map(session => (IConnectedViewerSession)session);

    public IReplaySessionStore ReplaySessionStore => _ReplaySessionStore;

    public IEncodedVisionContextConsumer VisionContextConsumer => _Root.VisionContextConsumer;

    public IBallFinder BallFinder => _Root.BallFinder;

    public IEncodedBallDetectionMaskDecoder BallDetectionMaskDecoder => _Root.BallDetectionMaskDecoder;

    public IEncodedReplayFrameDecoder ReplayFrameDecoder => _Root.ReplayFrameDecoder;

    public Task<RecorderConnectionResult> ConnectAsync(CancellationToken ct)
    {
        return _Root.ConnectAsync(ct);
    }

    public void Disconnect()
    {
        _Root.Disconnect();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        try
        {
            _Root.Dispose();
        }
        catch
        {
        }

        NetMQConfig.Cleanup();
    }
}
