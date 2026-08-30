// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;
using FoosVision.Ports.Vision;

namespace FoosVision.Adapters.Viewer.UnitTests.Session.Active.Fakes;

internal sealed class RecordingBallFinder : IBallFinder
{
    public int DetectCallCount { get; private set; }

    public IReadOnlyList<ObservedBall> Detect(byte[] frameBufferRGBA8888, TableConfiguration tableConfig)
    {
        DetectCallCount++;
        return [new ObservedBall(new Point(960 + (DetectCallCount * 10), 540), 0.8)];
    }

    public IReadOnlyList<ObservedBall> Detect(byte[] frameBufferRGBA8888, TableConfiguration tableConfig, Rectangle regionOfInterest)
    {
        return Detect(frameBufferRGBA8888, tableConfig);
    }

    public IReadOnlyList<ObservedBall> DetectYuv420(
        byte[] bufferY,
        byte[] bufferU,
        byte[] bufferV,
        int width,
        int height,
        int yRowStride,
        int yPixelStride,
        int uRowStride,
        int uPixelStride,
        int vRowStride,
        int vPixelStride,
        TableConfiguration tableConfig,
        Rectangle regionOfInterest)
    {
        DetectCallCount++;
        return [new ObservedBall(new Point(960 + (DetectCallCount * 10), 540), 0.8)];
    }
}
