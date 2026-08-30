// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Adapters.Viewer.Session.Active;
using FoosVision.Adapters.Viewer.Session.Overlays;
using FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;
using FoosVision.Protocol.Messages.LiveAnalysis;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active;

public class BallDetectionMaskTests
{
    [Fact]
    public void Handle_updates_overlay()
    {
        List<string> events = [];
        RecordingOverlaySink overlaySink = new(events);
        RecordingBallDetectionMaskDecoder decoder = new();
        BallDetectionMaskOverlayPresenter sut = new(
            overlaySink,
            decoder,
            () => false);

        byte[] buffer = [1, 2, 3, 4];
        sut.Handle(new BallDetectionMaskMessage
        {
            FrameId = 12,
            TimestampNs = 3_000_000_000,
            Width = 2,
            Height = 2,
            Buffer = buffer,
            Length = buffer.Length,
        });

        BallDetectionMaskOverlayState state = Assert.Single(overlaySink.BallDetectionMaskStates);
        Assert.Equal(12UL, state.FrameId);
        Assert.Equal(3_000_000_000, state.TimestampNs);
        Assert.Equal(2, state.Width);
        Assert.Equal(2, state.Height);
        Assert.Equal([1, 2, 3, 4], state.Buffer);
        Assert.Equal(buffer.Length, state.Length);
        Assert.Same(buffer, decoder.EncodedMasks.Single().Buffer);
    }

    [Fact]
    public void Handle_reuses_decode_buffer()
    {
        List<string> events = [];
        RecordingOverlaySink overlaySink = new(events);
        BallDetectionMaskOverlayPresenter sut = new(
            overlaySink,
            new RecordingBallDetectionMaskDecoder(),
            () => false);

        sut.Handle(new BallDetectionMaskMessage
        {
            FrameId = 12,
            TimestampNs = 3_000_000_000,
            Width = 2,
            Height = 2,
            Buffer = [1, 2, 3, 4],
            Length = 4,
        });
        sut.Handle(new BallDetectionMaskMessage
        {
            FrameId = 13,
            TimestampNs = 3_100_000_000,
            Width = 2,
            Height = 2,
            Buffer = [5, 6, 7, 8],
            Length = 4,
        });

        Assert.Equal(2, overlaySink.BallDetectionMaskStates.Count);
        Assert.Same(
            overlaySink.BallDetectionMaskStates[0].Buffer,
            overlaySink.BallDetectionMaskStates[1].Buffer);
    }

    [Fact]
    public void Handle_ignores_masks_while_replay_is_active()
    {
        List<string> events = [];
        RecordingOverlaySink overlaySink = new(events);
        RecordingBallDetectionMaskDecoder decoder = new();
        BallDetectionMaskOverlayPresenter sut = new(
            overlaySink,
            decoder,
            () => true);

        sut.Handle(new BallDetectionMaskMessage
        {
            Width = 2,
            Height = 2,
            Buffer = [1, 2, 3, 4],
            Length = 4,
        });

        Assert.Empty(overlaySink.BallDetectionMaskStates);
        Assert.Empty(decoder.EncodedMasks);
    }
}
