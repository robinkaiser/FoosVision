// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.TrackingCore.ValueObjects;

public enum Team
{
    /// <summary>
    /// Team A
    /// </summary>
    A,

    /// <summary>
    /// Team B
    /// </summary>
    B,

    /// <summary>
    /// No team
    /// </summary>
    None,
}

public enum PossessionArea
{
    /// <summary>
    /// Ball is in the defense area
    /// </summary>
    Defense,

    /// <summary>
    /// Ball is on the 5-bar
    /// </summary>
    FiveBar,

    /// <summary>
    /// Ball is on the 3-bar
    /// </summary>
    ThreeBar,

    /// <summary>
    /// Possession is unknown
    /// </summary>
    None,
}

/// <summary>
/// Ball possession
/// </summary>
/// <param name="Team">Team with possession</param>
/// <param name="Area">Area of possession</param>
public record struct BallPossession(Team Team, PossessionArea Area)
{
    /// <summary>
    /// Gets an unknown possession (no team and no area).
    /// </summary>
    public static BallPossession None { get; } = new(Team.None, PossessionArea.None);
}
