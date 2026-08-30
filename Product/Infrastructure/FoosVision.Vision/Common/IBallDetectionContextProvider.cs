// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.Common;

public interface IBallDetectionContextProvider
{
    /// <summary>
    /// Indicates that an input pixel should not be processed because it is outside of the
    /// playing field, belongs to rods or occlusions, or background was not initialized yet.
    /// </summary>
    const uint IgnoredPixel = 0xFF000000;

    /// <summary>
    /// Gets ball color dependent precalculated image that can be used for fast ball detection.
    /// In the white-ball scenario, each 32bpp pixel stores the accepted Y range used by ball
    /// finding during RGB-to-gray conversion: byte 0 is acceptedMinY, byte 1 is acceptedMaxY,
    /// and <see cref="IgnoredPixel"/> marks pixels that must not contribute to detection.
    /// </summary>
    byte[] ColorResponse32bpp { get; }

    /// <summary>
    /// Gets calibrated player color models used to suppress player-colored ball candidates.
    /// </summary>
    PlayerColorExclusionContext PlayerColorExclusion { get; }
}
