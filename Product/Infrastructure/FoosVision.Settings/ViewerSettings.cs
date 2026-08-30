// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

using FoosVision.Settings.Diagnostics;

namespace FoosVision.Settings;

public class ViewerSettings
{
    public ViewerDiagnosticsSettings Diagnostics { get; set; } = ViewerDiagnosticsSettings.CreateDefault();

    public static ViewerSettings CreateDefault()
    {
        return new ViewerSettings();
    }

    public void Validate()
    {
        Diagnostics ??= ViewerDiagnosticsSettings.CreateDefault();
        Diagnostics.Validate();
    }
}
