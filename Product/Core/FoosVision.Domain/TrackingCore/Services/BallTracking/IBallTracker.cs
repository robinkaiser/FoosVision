// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;
using FoosVision.Domain.Table.ValueObjects;
using FoosVision.Domain.TrackingCore.ValueObjects;

namespace FoosVision.Domain.TrackingCore.Services.BallTracking;

/// <summary>
/// Tracking snapshot
/// </summary>
public record TrackingSnapshot(Frame Frame, IReadOnlyList<TrackedBall> Candidates)
{
    public TrackedBall? Current => Candidates.Count > 0 ? Candidates[0] : null;

    public IEnumerable<TrackedBall> OtherCandidates => Candidates.Skip(1);
}

public interface IBallTracker
{
    /// <summary>Gets latest immutable snapshot. Null until the first frame is processed.</summary>
    TrackingSnapshot? Latest { get; }

    /// <summary>
    /// Process observations for given frame.
    /// </summary>
    /// <param name="frame">Frame to be processed.</param>
    /// <param name="observations">Observations for the given timestamp in no particular order.</param>
    /// <returns>Tracking snapshot.</returns>
    TrackingSnapshot ApplyObservations(Frame frame, IEnumerable<ObservedBall> observations);

    /// <summary>
    /// Update table configuration.
    /// </summary>
    /// <param name="tableConfig">Table configuration</param>
    void UpdateTableConfig(TableConfiguration tableConfig);
}
