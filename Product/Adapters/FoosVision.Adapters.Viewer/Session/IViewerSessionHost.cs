// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Connectivity;
using FoosVision.Common.Types;
using FoosVision.Ports.Media;
using FoosVision.Ports.Vision;
using FoosVision.UseCases.Replay.Ports;

namespace FoosVision.Adapters.Viewer.Session;

public interface IViewerSessionHost : IDisposable
{
    Option<IConnectedViewerSession> ConnectedSession { get; }

    IReplaySessionStore ReplaySessionStore { get; }

    IEncodedVisionContextConsumer VisionContextConsumer { get; }

    IBallFinder BallFinder { get; }

    IEncodedBallDetectionMaskDecoder BallDetectionMaskDecoder { get; }

    IEncodedReplayFrameDecoder ReplayFrameDecoder { get; }

    Task<RecorderConnectionResult> ConnectAsync(CancellationToken ct);

    void Disconnect();
}
