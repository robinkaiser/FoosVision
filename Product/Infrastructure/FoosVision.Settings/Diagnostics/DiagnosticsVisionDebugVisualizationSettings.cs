// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class DiagnosticsVisionDebugVisualizationSettings
{
    public bool ShowObservations { get; set; }

    public bool ShowBallDetectionMask { get; set; }

    public static DiagnosticsVisionDebugVisualizationSettings CreateDefault()
    {
        return new DiagnosticsVisionDebugVisualizationSettings();
    }

    public void Validate()
    {
    }
}
