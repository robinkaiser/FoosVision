// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class DiagnosticsVisionSettings
{
    public DiagnosticsVisionDebugVisualizationSettings DebugVisualizations { get; set; } =
        DiagnosticsVisionDebugVisualizationSettings.CreateDefault();

    public static DiagnosticsVisionSettings CreateDefault()
    {
        return new DiagnosticsVisionSettings();
    }

    public void Validate()
    {
        DebugVisualizations ??= DiagnosticsVisionDebugVisualizationSettings.CreateDefault();
        DebugVisualizations.Validate();
    }
}
