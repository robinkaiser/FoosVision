// SPDX-License-Identifier: GPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 Robin Kaiser

namespace FoosVision.Settings.Diagnostics;

public class DiagnosticsLoggingSettings
{
    public DiagnosticsFileLoggingSettings File { get; set; } = DiagnosticsFileLoggingSettings.CreateDefault();

    public DiagnosticsSeqLoggingSettings Seq { get; set; } = DiagnosticsSeqLoggingSettings.CreateDefault();

    public static DiagnosticsLoggingSettings CreateDefault()
    {
        return new DiagnosticsLoggingSettings();
    }

    public void Validate()
    {
        File ??= DiagnosticsFileLoggingSettings.CreateDefault();
        Seq ??= DiagnosticsSeqLoggingSettings.CreateDefault();

        File.Validate();
        Seq.Validate();
    }
}
