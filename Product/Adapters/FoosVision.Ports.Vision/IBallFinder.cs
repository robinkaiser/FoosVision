// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Ports.Vision;

public interface IBallFinder
{
    IReadOnlyList<ObservedBall> Detect(
        byte[] frameBufferRGBA8888,
        TableConfiguration tableConfig);

    IReadOnlyList<ObservedBall> Detect(
        byte[] frameBufferRGBA8888,
        TableConfiguration tableConfig,
        Rectangle regionOfInterest);

    IReadOnlyList<ObservedBall> DetectYuv420(
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
        Rectangle regionOfInterest);
}
