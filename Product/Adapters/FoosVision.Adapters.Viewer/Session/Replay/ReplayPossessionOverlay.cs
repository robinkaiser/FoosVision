// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Adapters.Viewer.Session.Replay;

internal readonly record struct ReplayPossessionOverlay(
    BallPossession AnchorPossession,
    long AnchorTimestampNs,
    int AnchorPossessionTimeMs);
