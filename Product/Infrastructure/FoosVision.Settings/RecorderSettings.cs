// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using System.Text.Json.Serialization;
using FoosVision.Settings.Diagnostics;

namespace FoosVision.Settings;

public class RecorderSettings
{
    public int Version { get; set; } = 1;

    [JsonRequired]
    public RecorderViewerSettings Viewer { get; set; } = RecorderViewerSettings.CreateDefault();

    [JsonRequired]
    public RecorderDiagnosticsSettings Diagnostics { get; set; } = RecorderDiagnosticsSettings.CreateDefault();

    public static RecorderSettings CreateDefault()
    {
        return new RecorderSettings();
    }

    public void Validate()
    {
        if (Version != 1)
        {
            throw new InvalidOperationException($"Unsupported settings config version '{Version}'.");
        }

        if (Viewer is null)
        {
            throw new InvalidOperationException($"{nameof(Viewer)} is required.");
        }

        if (Viewer is null)
        {
            throw new InvalidOperationException($"{nameof(Viewer)} is required.");
        }

        Viewer.Validate();
        Diagnostics.Validate();
    }
}
