// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Common.Logging;
using FoosVision.Ports.Vision;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Viewer.Session.Active;

internal class BallDetectionMaskOverlayPresenter
{
    private static readonly Source _Log = new("Viewer.Session.Active.BallDetectionMaskOverlayPresenter");

    private readonly IOverlaySink _OverlaySink;
    private readonly IEncodedBallDetectionMaskDecoder _Decoder;
    private readonly Func<bool> _IsReplayActive;
    private byte[] _DecodeBuffer = [];

    public BallDetectionMaskOverlayPresenter(
        IOverlaySink overlaySink,
        IEncodedBallDetectionMaskDecoder decoder,
        Func<bool> isReplayActive)
    {
        _OverlaySink = overlaySink;
        _Decoder = decoder;
        _IsReplayActive = isReplayActive;
    }

    public void Handle(BallDetectionMaskMessage message)
    {
        if (_IsReplayActive())
        {
            return;
        }

        if (message.Width <= 0 ||
            message.Height <= 0 ||
            message.Length < 0 ||
            message.Length > message.Buffer.Length)
        {
            _Log.Warning("Ball detection mask ignored because the payload is invalid.");
            return;
        }

        if (message.Width > int.MaxValue / message.Height)
        {
            _Log.Warning("Ball detection mask ignored because the payload dimensions are invalid.");
            return;
        }

        int pixelCount = message.Width * message.Height;

        if (_DecodeBuffer.Length < pixelCount)
        {
            _DecodeBuffer = new byte[pixelCount];
        }

        EncodedBallDetectionMask encodedMask = new(
            message.Buffer,
            message.Length,
            message.Width,
            message.Height);

        _Decoder.DecodeBallDetectionMask(encodedMask, _DecodeBuffer);

        BallDetectionMaskOverlayState state = new(
            message.FrameId,
            message.TimestampNs,
            message.Width,
            message.Height,
            _DecodeBuffer,
            pixelCount);

        _OverlaySink.UpdateBallDetectionMaskState(state);
    }
}
