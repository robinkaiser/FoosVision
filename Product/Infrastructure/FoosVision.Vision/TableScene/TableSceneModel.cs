// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Vision.TableScene;

public enum BackgroundPixelState : byte
{
    /// <summary>
    /// Pixels that will not be processed because they are outside the playing field or belong to rods or occlusions.
    /// </summary>
    IgnoredPixel = 0x00,

    /// <summary>
    /// Pixels that will be processed and can be classified as foreground.
    /// </summary>
    Ok = 0x01,

    /// <summary>
    /// Pixels that will not be processed because the background is currently adapting (upper).
    /// </summary>
    AdaptingUpper = 0x02,

    /// <summary>
    /// Pixels that will not be processed because the background is currently adapting (lower).
    /// </summary>
    AdaptingLower = 0x03,

    /// <summary>
    /// Pixels that are still uninitialized.
    /// </summary>
    NotInitialized = 0x04,
}

public class TableSceneModel
{
    /// <summary>
    /// Background update: Input pixel that will not be processed for update because they are part of
    /// the player or the detected ball.
    /// </summary>
    public const uint RgbaIgnoredPixel = 0xFF000000;

    public TableSceneModel(int maxWidth, int maxHeight)
    {
        int size = maxWidth * maxHeight;

        StateImage = new byte[size];
        MinImage = new byte[size * 4];
        MaxImage = new byte[size * 4];

        Array.Fill(StateImage, (byte)BackgroundPixelState.NotInitialized);
    }

    public byte[] StateImage { get; }

    public byte[] MinImage { get; }

    public byte[] MaxImage { get; }
}
