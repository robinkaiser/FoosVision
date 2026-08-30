// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Protocol.Messages.Live;

namespace FoosVision.Adapters.Viewer.Session.Overlays;

public record TableOverlayState(
    TrapeziumOverlay Boundary,
    IReadOnlyList<TableBarOverlay> Bars,
    IReadOnlyList<TrapeziumOverlay> Occlusions)
{
    public static TableOverlayState Empty { get; } = new(
        Boundary: new TrapeziumOverlay(
            UpperLeft: new OverlayPoint(0f, 0f),
            UpperRight: new OverlayPoint(1f, 0f),
            LowerLeft: new OverlayPoint(0f, 1f),
            LowerRight: new OverlayPoint(1f, 1f)),
        Bars: [],
        Occlusions: []);
}

public record TrapeziumOverlay(
    OverlayPoint UpperLeft,
    OverlayPoint UpperRight,
    OverlayPoint LowerLeft,
    OverlayPoint LowerRight);

public record TableBarOverlay(
    BarTypeMessage Type,
    LineOverlay Center,
    uint TeamArgb);

public record LineOverlay(OverlayPoint P0, OverlayPoint P1);
