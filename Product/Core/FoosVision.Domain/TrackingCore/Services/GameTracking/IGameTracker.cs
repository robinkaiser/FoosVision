// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.GameTracking;

public interface IGameTracker
{
    GameTrackingSnapshot? Latest { get; }

    GameTrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations);

    void UpdateTableConfig(TableConfiguration tableConfig);
}
