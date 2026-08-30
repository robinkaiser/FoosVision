// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Media.Android.CameraFeed;

internal sealed record AndroidHighSpeedProfile(
    string CameraId,
    int Width,
    int Height,
    int PreviewFps,
    int SlowMoFps);
