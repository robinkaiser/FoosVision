// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Domain.Capture.ValueObjects;

public enum CameraType
{
    /// <summary>
    /// Invalid camera
    /// </summary>
    None,

    /// <summary>
    /// Only main (wide) cameras are supported, Ultra-wide and Tele are not.
    /// </summary>
    Main,
}

public enum CameraFieldOfView
{
    /// <summary>
    /// The entire table is visible. The camera is positioned above the table, ideally in the center.
    /// Small tilt angles are permitted so that the table light does not run centrally.
    /// </summary>
    FullTableFromAbove,
}

public enum CameraResolution
{
    /// <summary>
    /// Invalid camera
    /// </summary>
    None,

    /// <summary>
    /// 1920 x 1080
    /// </summary>
    FullHD,
}

/// <summary>
/// Camera profile
/// </summary>
/// <param name="Type">Camera type used</param>
/// <param name="FieldOfView">Field of view used</param>
/// <param name="Resolution">Resolution used</param>
/// <param name="ProcessingFps">Lower rate for real-time ball detection</param>
/// <param name="HighFps">High rate for offline anlysis, slow motion replay and live stream</param>
public record class CameraProfile(CameraType Type,
    CameraFieldOfView FieldOfView,
    CameraResolution Resolution,
    int ProcessingFps,
    int HighFps);
