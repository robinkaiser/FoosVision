// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Adapters.Viewer.Session.Overlays;

public record BallDetectionMaskOverlayState(
    ulong FrameId,
    long TimestampNs,
    int Width,
    int Height,
    byte[] Buffer,
    int Length);
