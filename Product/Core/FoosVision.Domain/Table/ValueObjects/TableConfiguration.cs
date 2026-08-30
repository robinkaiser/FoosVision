// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Common.Types;

namespace FoosVision.Domain.Table.ValueObjects;

/*
FOOSBALL TABLE — IMAGE COORDINATE SYSTEM
(0,0) is the top-left pixel of the image. x increases to the right, y increases downward.
For FullHD: W = 1920, H = 1080, X_Max = 1919, Y_Max = 1079

   (0,0)  +----------------------------------------------------------------+
          |                                                                |
          |       |      |      |      |      |      |      |      |       |
          |       |      |      |      X      X      |      |      |       |
          |       |      |      X      |      |      X      |      |       |
          |  |--  |      X      |      X      X      |      X      |  --|  |
          |  |    |      |      |      |      |      |      |      |    |  |
          |  |    X      |      X      X      X      X      |      X    |  |
          |  |    |      |      |      |      |      |      |      |    |  |
          |  |--  |      X      |      X      X      |      X      |  --|  |
          |       |      |      X      |      |      X      |      |       |
          |       |      |      |      X      X      |      |      |       |
          |       |      |      |      |      |      |      |      |       |
          |                                                                |
          +----------------------------------------------------------------+ (W,H)
                  A      A      B      A      B      A      B      B
                  1      2      3      5      5      3      2      1
*/

/// <summary>
/// Bar types are sorted, starting on the left side at low x-coordinate.
/// </summary>
public enum BarType
{
    /// <summary>
    /// Goalie bar of A team
    /// </summary>
    A1,

    /// <summary>
    /// 2-bar
    /// </summary>
    A2,

    /// <summary>
    /// 3-bar
    /// </summary>
    B3,

    /// <summary>
    /// 5-bar
    /// </summary>
    A5,

    /// <summary>
    /// 5-bar
    /// </summary>
    B5,

    /// <summary>
    /// 3-bar
    /// </summary>
    A3,

    /// <summary>
    /// 2-bar
    /// </summary>
    B2,

    /// <summary>
    /// Goalie bar of B team
    /// </summary>
    B1,
}

/// <summary>
/// Table bar (rod)
/// </summary>
/// <param name="type">Bar type</param>
/// <param name="Left">Left bar boundary</param>
/// <param name="Center">Center bar line, middle of left and right</param>
/// <param name="Right">Right bar boundary</param>
public record Bar(BarType Type, Line Left, Line Center, Line Right);

/// <summary>
/// Table bars
/// </summary>
/// <param name="A1">Bar</param>
/// <param name="A2">Bar</param>
/// <param name="B3">Bar</param>
/// <param name="A5">Bar</param>
/// <param name="B5">Bar</param>
/// <param name="A3">Bar</param>
/// <param name="B2">Bar</param>
/// <param name="B1">Bar</param>
public record TableBars(Bar A1, Bar A2, Bar B3, Bar A5, Bar B5, Bar A3, Bar B2, Bar B1)
{
    public IEnumerable<Bar> All => [A1, A2, B3, A5, B5, A3, B2, B1];

    public Bar this[BarType t] => t switch
    {
        BarType.A1 => A1,
        BarType.A2 => A2,
        BarType.B3 => B3,
        BarType.A5 => A5,
        BarType.B5 => B5,
        BarType.A3 => A3,
        BarType.B2 => B2,
        BarType.B1 => B1,
        _ => throw new ArgumentOutOfRangeException(nameof(t))
    };
}

/// <summary>
/// Valid playing field.
/// The boundary trapezium describes the inner playing surface in image coordinates.
/// The upper and lower edges are the detected horizontal field boundaries; the left
/// and right edges are the inner goalie-rod boundaries, currently A1.Right and B1.Left.
/// UpperLeft/UpperRight are the intersections with the upper boundary, LowerLeft/LowerRight
/// are the intersections with the lower boundary.
/// </summary>
/// <param name="Boundary">Table boundary</param>
/// <param name="Bars">Table bars</param>
/// <param name="Occlusions">Image-space regions where objects obstruct the view onto the playing field.</param>
public record PlayingField(Trapezium Boundary, TableBars Bars, IReadOnlyList<Trapezium> Occlusions);

/// <summary>
/// Colors of the players.
/// </summary>
public readonly record struct PlayerColors(uint TeamAArgb, uint TeamBArgb);

/// <summary>
/// Colors of the players.
/// </summary>
public enum BallColor
{
    /// <summary>
    /// Ball color is unknown
    /// </summary>
    Unknown,

    /// <summary>
    /// White
    /// </summary>
    White,

    /// <summary>
    /// Yellow
    /// </summary>
    Yellow,

    /// <summary>
    /// Red
    /// </summary>
    Red,
}

/// <summary>
/// Valid table configuration
/// </summary>
/// <param name="Field">Playing field</param>
/// <param name="Players">Table players</param>
/// <param name="Bars">Table bars</param>
public record TableConfiguration(PlayingField Field, PlayerColors Players, BallColor Ball);
