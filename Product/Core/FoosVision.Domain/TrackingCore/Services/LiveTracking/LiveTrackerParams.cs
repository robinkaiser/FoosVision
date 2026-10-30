// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.TrackingCore.Services.LiveTracking;

public record class LiveTrackerParams
{
    public TimeSpan PossessionHoldTime { get; init; } = TimeSpan.FromMilliseconds(2000);

    public static readonly LiveTrackerParams Default = new();
}
